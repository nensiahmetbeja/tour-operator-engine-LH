using System.Security.Claims;
using Lufthansa.Application.Services;
using Lufthansa.Application.TourOperators.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lufthansa.Api.Controllers;

[ApiController]
[Route("api/touroperators")]
public class TourOperatorsController(ITourOperatorService service) : ControllerBase
{
    // Admin only: list all
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await service.GetAllAsync(ct));

    // Admin or operator (owner-only)
    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (User.IsInRole("TourOperator"))
        {
            var claimId = User.FindFirst("tourOperatorId")?.Value;
            if (!Guid.TryParse(claimId, out var myId) || myId != id)
                return Forbid(); // or Unauthorized
        }

        var dto = await service.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // Admin only: create
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTourOperatorRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException) // code exists
        {
            return Conflict(new { message = "TourOperator code already exists." });
        }
    }

    // Admin only: delete
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await service.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}