using Lufthansa.Application.Data.DTOs;
using Lufthansa.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lufthansa.Api.Controllers;

[ApiController]
[Route("api/data")]
public sealed class AdminDataController(IPricingQueryService svc) : ControllerBase
{
    // GET /api/data/{tourOperatorId}?from=2025-12-01&to=2025-12-31&page=1&pageSize=50
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("{tourOperatorId:guid}")]
    public async Task<IActionResult> Get(Guid tourOperatorId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var data = await svc.GetAsync(new PricingQueryDto(tourOperatorId, from, to, page, pageSize), ct);
        return Ok(data);
    }
}