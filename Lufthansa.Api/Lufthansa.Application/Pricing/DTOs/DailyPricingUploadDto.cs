namespace Lufthansa.Application.Pricing.DTOs;

public sealed record DailyPricingUploadDto(
    string RouteCode,
    string SeasonCode,
    DateOnly Date,
    decimal EconomyPrice,
    decimal BusinessPrice,
    int EconomySeats,
    int BusinessSeats
);

public sealed record DailyPricingUploadRaw(
    string RouteCode,
    string SeasonCode,
    string Date,
    string EconomyPrice,
    string BusinessPrice,
    string EconomySeats,
    string BusinessSeats
);

