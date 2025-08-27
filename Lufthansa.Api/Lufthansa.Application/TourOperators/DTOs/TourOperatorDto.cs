namespace Lufthansa.Application.TourOperators.DTOs;

public sealed record TourOperatorDto(Guid Id, string Name, string Code, DateTime CreatedAt);