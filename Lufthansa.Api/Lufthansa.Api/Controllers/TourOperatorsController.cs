using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lufthansa.Api.Controllers;

[ApiController]
[Route("api/touroperators")]
public class TourOperatorsController : ControllerBase
{
    // Only admins can list all operators:
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public IActionResult GetAll() => Ok(new[] { "placeholder" });

    // Only tour operators (later + owner check) can do their action:
    [Authorize(Policy = "TourOperatorOnly")]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { tourOperatorId = User.FindFirst("tourOperatorId")?.Value });
}