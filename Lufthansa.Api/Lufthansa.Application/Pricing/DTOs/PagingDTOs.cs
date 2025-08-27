namespace Lufthansa.Application.Data.DTOs;

public sealed record PricingQueryDto(Guid TourOperatorId, int Page = 1, int PageSize = 50);
public sealed record PricingRowDto(DateOnly Date, string RouteCode, string SeasonCode,
    decimal EconomyPrice, decimal BusinessPrice, int EconomySeats, int BusinessSeats);

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);