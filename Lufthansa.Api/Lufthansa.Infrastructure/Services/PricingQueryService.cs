using System.Text.Json;
using System.Text.Json.Serialization;
using Lufthansa.Application.Data.DTOs;
using Lufthansa.Application.Services;
using Lufthansa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace Lufthansa.Infrastructure.Services;

public sealed class PricingQueryService(
    ApplicationDbContext db,
    IDistributedCache cache,
    IConfiguration config) : IPricingQueryService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new DateOnlyConverter() }
    };

    public async Task<PagedResultDto<PricingRowDto>> GetAsync(PricingQueryDto q, CancellationToken ct = default)
    {
        // 1) Build a versioned cache key (no date range in DTO anymore)
        var version = await cache.GetStringAsync(VersionKey(q.TourOperatorId), ct) ?? "0";
        var key = CacheKey(version, q);

        // 2) Try cache first
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<PagedResultDto<PricingRowDto>>(cached, JsonOpts)!;

        // 3) DB query (filters + pagination)
        var baseQ =
            from p in db.DailyPricings.AsNoTracking()
            join r  in db.Routes.AsNoTracking()   on p.RouteId  equals r.Id
            join se in db.Seasons.AsNoTracking()  on p.SeasonId equals se.Id
            where p.TourOperatorId == q.TourOperatorId
            select new
            {
                p.Date,
                RouteCode   = r.Code,
                SeasonCode  = se.Code,
                p.EconomyPrice,
                p.BusinessPrice,
                p.EconomySeats,
                p.BusinessSeats
            };

        var total = await baseQ.CountAsync(ct);

        var items = await baseQ
            .OrderBy(x => x.Date)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(x => new PricingRowDto(
                x.Date, x.RouteCode, x.SeasonCode,
                x.EconomyPrice, x.BusinessPrice, x.EconomySeats, x.BusinessSeats))
            .ToListAsync(ct);

        var result = new PagedResultDto<PricingRowDto>(items, total, q.Page, q.PageSize);

        // 4) Store in cache (short TTL)
        var ttl = int.TryParse(config["Redis:DefaultTtlSeconds"], out var s) ? s : 60;
        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(result, JsonOpts),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl)
            },
            ct);

        return result;
    }

    private static string VersionKey(Guid tourOperatorId) => $"pricing:v:{tourOperatorId}";

    // Removed from/to from the cache key since they no longer exist on the DTO
    private static string CacheKey(string ver, PricingQueryDto q) =>
        $"pricing:{ver}:{q.TourOperatorId}:{q.Page}:{q.PageSize}";

    // Minimal DateOnly converter for System.Text.Json
    private sealed class DateOnlyConverter : JsonConverter<DateOnly>
    {
        private const string F = "yyyy-MM-dd";
        public override DateOnly Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) =>
            DateOnly.ParseExact(r.GetString()!, F);
        public override void Write(Utf8JsonWriter w, DateOnly v, JsonSerializerOptions o) =>
            w.WriteStringValue(v.ToString(F));
    }
}
