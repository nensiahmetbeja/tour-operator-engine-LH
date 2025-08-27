using CsvHelper;
using CsvHelper.Configuration;
using Lufthansa.Application.Pricing.DTOs;
using Lufthansa.Application.Services;
using Lufthansa.Domain.Entities;
using Lufthansa.Infrastructure.Persistence;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using Microsoft.Extensions.Logging; // added

public class PricingUploadService : IPricingUploadService
{
    private readonly ApplicationDbContext db;
    private readonly IDistributedCache cache;
    private readonly IProgressNotifier notifier;
    private readonly ILogger<PricingUploadService> logger; // added

    public PricingUploadService(ApplicationDbContext db, IDistributedCache cache, IProgressNotifier notifier, ILogger<PricingUploadService> logger) // added logger
    {
        this.db = db;
        this.cache = cache;
        this.notifier = notifier;
        this.logger = logger; 
    }

    private static readonly string DateFormat = "yyyy-MM-dd";
    private Task Report(string? connId, string stage, int? pct, string message, CancellationToken ct = default) =>
        notifier.ReportAsync(connId, stage, pct, message, ct);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";

    public async Task<UploadSummaryDto> UploadPricingAsync(
        Guid tourOperatorId,
        Stream csvStream,
        string? connectionId = null,
        bool skipBadRows = true,
        string mode = "error",
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Pricing upload started {@UploadContext}",
            new { TourOperatorId = tourOperatorId, ConnectionId = connectionId, Mode = mode, SkipBadRows = skipBadRows }); // added

        try
        {
            // --- 0) Optionally pre-count rows for % (only if the stream can seek) ---
            int totalRows = 0;
            if (csvStream.CanSeek)
            {
                var pos = csvStream.Position;
                using (var counterReader = new StreamReader(csvStream, leaveOpen: true))
                {
                    // Count lines; subtract 1 for header if present
                    while (await counterReader.ReadLineAsync() is { } _) totalRows++;
                    if (totalRows > 0) totalRows--; // header
                }
                csvStream.Position = pos; // rewind
                logger.LogDebug("Row pre-count completed: TotalRows={TotalRows}, CanSeek={CanSeek}", totalRows, true); // added
            }
            else
            {
                logger.LogDebug("Row pre-count skipped: stream is not seekable"); // added
            }

            await Report(connectionId, "validation_started", 0, "Validation started", ct);
            logger.LogInformation("Validation started for operator {OperatorId}", tourOperatorId); // added

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

        var row = 1;          // header
        var processed = 0;    // valid rows parsed

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

                processed++;

                // Report every 500 rows
                if (processed % 500 == 0)
                {
                    int? pct = null;
                    if (totalRows > 0) pct = Math.Min(99, (int)Math.Round(processed * 100.0 / totalRows));
                        await Report(connectionId, "processing", pct, $"Processed {processed} rows…", ct);
                        logger.LogInformation("Validation progress: Processed={Processed}/{TotalRows}", processed, totalRows); // added
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"Row {row}: {ex.Message}");
                    logger.LogWarning(ex, "Validation error at row {Row}: {Error}", row, ex.Message); // added
                    if (!skipBadRows) break; // stop on first error (all-or-nothing)
                }
            }

            // Final validation update (if we didn’t hit the 500-row boundary)
            {
                int? pct = totalRows > 0 ? Math.Min(99, (int)Math.Round(processed * 100.0 / totalRows)) : null;
                await Report(connectionId, "processing", pct, $"Processed {processed} rows (validation complete).", ct);
                logger.LogInformation("Validation completed: Processed={Processed}, Skipped={Skipped}, Errors={ErrorCount}", processed, skipped, errors.Count); // added
            }

            var inserted = 0;
            if (toInsert.Count > 0)
            {
                await Report(connectionId, "bulk_insert_started", 99, "Bulk insert started…", ct);
                logger.LogInformation("Bulk insert started: RowsToInsert={RowsToInsert}", toInsert.Count); // added

                db.DailyPricings.AddRange(toInsert);
                try
                {
                    inserted = await db.SaveChangesAsync(ct);

                    await InvalidateOperatorCache(tourOperatorId, ct);
                    logger.LogDebug("Cache invalidated for operator {OperatorId}", tourOperatorId); // added

                    await Report(connectionId, "bulk_insert_completed", 100, $"Bulk insert completed. Inserted {inserted}, skipped {skipped}.", ct);
                    logger.LogInformation("Bulk insert completed: Inserted={Inserted}, Skipped={Skipped}", inserted, skipped); // added
                    return new UploadSummaryDto(inserted, skipped, errors);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    logger.LogInformation(ex, "Unique constraint violation on bulk insert. Falling back to per-row handling with mode={Mode}", mode); // added
                    // Fall back to per-row handling
                }

                // Clean tracker before per-row loop
                db.ChangeTracker.Clear();

                var handled = 0;
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
                                    db.DailyPricings.Add(e);
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
                                throw new ArgumentException("Duplicate day detected. Re-upload with ?mode=skip or ?mode=overwrite.");

                            default:
                                db.DailyPricings.Add(e);
                                await db.SaveChangesAsync(ct);
                                inserted++;
                                break;
                        }

                        handled++;
                        if (handled % 1000 == 0)
                        {
                            await Report(connectionId, "bulk_insert_progress", null, $"Saved {handled}/{toInsert.Count}…", ct);
                            logger.LogInformation("Per-row insert progress: Saved={Saved}/{Total}", handled, toInsert.Count); // added
                        }
                    }
                    catch (DbUpdateException ex) when (IsUniqueViolation(ex) && mode == "skip")
                    {
                        skipped++;
                        logger.LogDebug(ex, "Duplicate row skipped during per-row insert (mode=skip). Operator={OperatorId}, Date={Date}, RouteId={RouteId}, SeasonId={SeasonId}",
                            e.TourOperatorId, e.Date, e.RouteId, e.SeasonId); // added
                        db.ChangeTracker.Clear();
                    }
                }

                if (inserted > 0)
                {
                    await InvalidateOperatorCache(tourOperatorId, ct);
                    logger.LogDebug("Cache invalidated for operator {OperatorId} after per-row operations", tourOperatorId); // added
                }

                await Report(connectionId, "bulk_insert_completed", 100, $"Bulk insert completed. Inserted {inserted}, skipped {skipped}.", ct);
                logger.LogInformation("Bulk insert completed (per-row): Inserted={Inserted}, Skipped={Skipped}", inserted, skipped); // added
            }
            else
            {
                await Report(connectionId, "done", 100, $"No valid rows. Skipped {skipped}.", ct);
                logger.LogInformation("No valid rows parsed. Skipped={Skipped}, Errors={ErrorCount}", skipped, errors.Count); // added
            }

            logger.LogInformation("Pricing upload completed {@UploadResult}", new { TourOperatorId = tourOperatorId, Inserted = inserted, Skipped = skipped, Errors = errors.Count }); // added
            return new UploadSummaryDto(inserted, skipped, errors);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Pricing upload canceled for operator {OperatorId}, connection {ConnectionId}", tourOperatorId, connectionId); // added
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "System error during pricing upload for operator {OperatorId}, connection {ConnectionId}", tourOperatorId, connectionId); // added
            throw;
        }
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
        logger.LogDebug("Route created: Operator={OperatorId}, Code={Code}, RouteId={RouteId}", opId, code, entity.Id); // added
        return entity.Id;
    }

    private async Task<Guid> GetOrCreateSeasonId(Guid opId, string code, CancellationToken ct)
    {
        var se = await db.Seasons.AsNoTracking()
            .Where(x => x.TourOperatorId == opId && x.Code == code)
            .Select(x => new { x.Id }).FirstOrDefaultAsync(ct);
        if (se is not null) return se.Id;

        var entity = new Season { TourOperatorId = opId, Code = code };
        db.Seasons.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogDebug("Season created: Operator={OperatorId}, Code={Code}, SeasonId={SeasonId}", opId, code, entity.Id); // added
        return entity.Id;
    }

    private Task InvalidateOperatorCache(Guid opId, CancellationToken ct) =>
        cache.SetStringAsync($"pricing:v:{opId}", DateTime.UtcNow.Ticks.ToString(), ct);
}
