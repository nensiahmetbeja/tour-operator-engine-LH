using Lufthansa.Application.Pricing.DTOs;

namespace Lufthansa.Application.Services;

public interface IPricingUploadService
{
    // Task<int> UploadPricingAsync(Guid tourOperatorId, Stream csvStream, CancellationToken ct = default);
    Task<UploadSummaryDto> UploadPricingAsync(Guid tourOperatorId, Stream csvStream, bool skipBadRows = true, string mode = "error",
        CancellationToken ct = default);

}