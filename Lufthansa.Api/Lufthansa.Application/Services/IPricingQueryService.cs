using Lufthansa.Application.Data.DTOs;

namespace Lufthansa.Application.Services;


public interface IPricingQueryService
{
    Task<PagedResultDto<PricingRowDto>> GetAsync(PricingQueryDto query, CancellationToken ct = default);
}
