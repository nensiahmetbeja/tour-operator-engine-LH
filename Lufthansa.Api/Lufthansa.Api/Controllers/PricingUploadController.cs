using System.Security.Claims;
using Lufthansa.Api.Controllers.Requests;
using Lufthansa.Application.Services;
using Lufthansa.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lufthansa.Api.Controllers;

[ApiController]
[Route("api/touroperators/{tourOperatorId:guid}/pricing-upload")]
public class PricingUploadController(IPricingUploadService service) : ControllerBase
{
    [Authorize(Policy = "TourOperatorOnly")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid tourOperatorId,  [FromForm] PricingUploadRequest request, 
        [FromQuery] string? connectionId,      //  supplied from client
        [FromQuery] bool skipBadRows = true, 
        [FromQuery] string mode = "skip",  // "skip" | "overwrite" | "error"
        CancellationToken ct = default)
    {
        var claimId = User.FindFirst("tourOperatorId")?.Value;
        if (!Guid.TryParse(claimId, out var myId) || myId != tourOperatorId)
            return Forbid();
        var file = request.File;

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "CSV file is required" });

        var summary = await service.UploadPricingAsync(
            tourOperatorId, 
            file.OpenReadStream(),
            connectionId,
            skipBadRows: true, mode: mode, 
            ct: ct);
        
        return Ok(summary);
    }

}