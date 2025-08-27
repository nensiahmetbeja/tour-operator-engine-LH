using CsvHelper;
using CsvHelper.Configuration;
using Lufthansa.Application.Pricing.DTOs;
using Lufthansa.Application.Services;
using Lufthansa.Domain.Entities;
using Lufthansa.Infrastructure.Persistence;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class PricingUploadService(ApplicationDbContext db) : IPricingUploadService
{
    private static readonly string DateFormat = "yyyy-MM-dd";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";

    // mode parameter ("skip" | "overwrite" | "error")
    public async Task<UploadSummaryDto> UploadPricingAsync(
        Guid tourOperatorId,
        Stream csvStream,
        bool skipBadRows = true,
        string mode = "error",
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = a => a.Header.Trim()
        };

        using var csv = new CsvReader(reader, cfg);

        var toInsert = new List<DailyPricing>();
        var errors   = new List<string>();
        var skipped  = 0;

        // Read header
        csv.Read();
        csv.ReadHeader();

        var row = 1; // header
        while (await csv.ReadAsync())
        {
            row++;
            try
            {
                var routeCode     = csv.GetField("RouteCode")?.Trim();
                var seasonCode    = csv.GetField("SeasonCode")?.Trim();
                var dateText      = csv.GetField("Date")?.Trim();
                var econPriceText = csv.GetField("EconomyPrice")?.Trim();
                var bizPriceText  = csv.GetField("BusinessPrice")?.Trim();
                var econSeatsText = csv.GetField("EconomySeats")?.Trim();
                var bizSeatsText  = csv.GetField("BusinessSeats")?.Trim();

                if (string.IsNullOrWhiteSpace(routeCode))  throw new Exception("RouteCode is required.");
                if (string.IsNullOrWhiteSpace(seasonCode)) throw new Exception("SeasonCode is required.");
                if (string.IsNullOrWhiteSpace(dateText))   throw new Exception("Date is required.");

                if (!DateOnly.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    throw new Exception($"Invalid Date '{dateText}'. Expected {DateFormat}.");

                if (!decimal.TryParse(econPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var econPrice))
                    throw new Exception($"EconomyPrice invalid '{econPriceText}'.");
                if (!decimal.TryParse(bizPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var bizPrice))
                    throw new Exception($"BusinessPrice invalid '{bizPriceText}'.");
                if (!int.TryParse(econSeatsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var econSeats))
                    throw new Exception($"EconomySeats invalid '{econSeatsText}'.");
                if (!int.TryParse(bizSeatsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bizSeats))
                    throw new Exception($"BusinessSeats invalid '{bizSeatsText}'.");

                if (econPrice < 0 || bizPrice < 0) throw new Exception("Prices must be >= 0.");
                if (econSeats < 0 || bizSeats < 0) throw new Exception("Seats must be >= 0.");

                var routeId  = await GetOrCreateRouteId(tourOperatorId, routeCode!, ct);
                var seasonId = await GetOrCreateSeasonId(tourOperatorId, seasonCode!, ct);

                toInsert.Add(new DailyPricing
                {
                    TourOperatorId = tourOperatorId,
                    RouteId = routeId,
                    SeasonId = seasonId,
                    Date = date,
                    EconomyPrice = econPrice,
                    BusinessPrice = bizPrice,
                    EconomySeats = econSeats,
                    BusinessSeats = bizSeats,
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                skipped++;
                errors.Add($"Row {row}: {ex.Message}");
                if (!skipBadRows) break; // stop on first error (all-or-nothing)
            }
        }

        var inserted = 0;
        if (toInsert.Count > 0)
        {
            // Fast path: try to insert everything in one batch
            db.DailyPricings.AddRange(toInsert);
            try
            {
                inserted = await db.SaveChangesAsync(ct);
                return new UploadSummaryDto(inserted, skipped, errors);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // fall back to per-row handling
            }

            // Clean tracker before per-row loop
            db.ChangeTracker.Clear();

            foreach (var e in toInsert)
            {
                try
                {
                    switch (mode)
                    {
                        case "skip":
                            db.DailyPricings.Add(e);
                            await db.SaveChangesAsync(ct);
                            inserted++;
                            break;

                        case "overwrite":
                        {
                            var existing = await db.DailyPricings.FirstOrDefaultAsync(x =>
                                x.TourOperatorId == e.TourOperatorId &&
                                x.RouteId        == e.RouteId &&
                                x.SeasonId       == e.SeasonId &&
                                x.Date           == e.Date, ct);

                            if (existing is null)
                            {
                                db.DailyPricings.Add(e);
                            }
                            else
                            {
                                existing.EconomyPrice  = e.EconomyPrice;
                                existing.BusinessPrice = e.BusinessPrice;
                                existing.EconomySeats  = e.EconomySeats;
                                existing.BusinessSeats = e.BusinessSeats;
                            }

                            await db.SaveChangesAsync(ct);
                            inserted++;
                            break;
                        }

                        case "error":
                            throw new ArgumentException(
                                "Duplicate day detected for this route/season/date. Re-upload with ?mode=skip or ?mode=overwrite.");

                        default:
                            // safety: treat unknown as skip
                            db.DailyPricings.Add(e);
                            await db.SaveChangesAsync(ct);
                            inserted++;
                            break;
                    }
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex) && mode == "skip")
                {
                    // Duplicate → skip gracefully
                    skipped++;
                    // Optional: add more detail if you want
                    // errors.Add($"Duplicate skipped: {e.Date:yyyy-MM-dd}");
                    db.ChangeTracker.Clear();
                }
            }
        }

        return new UploadSummaryDto(inserted, skipped, errors);
    }
    private async Task<Guid> GetOrCreateRouteId(Guid opId, string code, CancellationToken ct)
    {
        var r = await db.Routes.AsNoTracking()
            .Where(x => x.TourOperatorId == opId && x.Code == code)
            .Select(x => new { x.Id }).FirstOrDefaultAsync(ct);
        if (r is not null) return r.Id;

        var entity = new Route { TourOperatorId = opId, Code = code };
        db.Routes.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }

    private async Task<Guid> GetOrCreateSeasonId(Guid opId, string code, CancellationToken ct)
    {
        var s = await db.Seasons.AsNoTracking()
            .Where(x => x.TourOperatorId == opId && x.Code == code)
            .Select(x => new { x.Id }).FirstOrDefaultAsync(ct);
        if (s is not null) return s.Id;

        var entity = new Season { TourOperatorId = opId, Code = code };
        db.Seasons.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
