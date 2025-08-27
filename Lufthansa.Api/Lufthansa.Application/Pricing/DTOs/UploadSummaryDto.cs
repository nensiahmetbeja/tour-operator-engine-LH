namespace Lufthansa.Application.Pricing.DTOs;

public sealed record UploadSummaryDto(int Inserted, int Skipped, List<string> Errors);
