using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Lufthansa.Api.Auth;
using Lufthansa.Api.Auth.Blacklist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Lufthansa.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IConfiguration cfg, IUserService users, ITokenBlacklist blacklist) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var (ok, role, tourOpId) = users.ValidateUser(req.Username, req.Password);
        if (!ok) return Unauthorized(new { error = "Invalid credentials" });

        var jwtCfg = cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtCfg["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, req.Username),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (tourOpId.HasValue)
            claims.Add(new Claim("tourOperatorId", tourOpId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: jwtCfg["Issuer"],
            audience: jwtCfg["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(jwtCfg["AccessTokenMinutes"]!)),
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { access_token = jwt, role, tourOperatorId = tourOpId });
    }

    [Authorize] // any logged-in user
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // blacklist current token by jti
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!string.IsNullOrEmpty(jti))
        {
            // TTL = remaining lifetime; for demo, 60 minutes
            await blacklist.BanAsync(jti, TimeSpan.FromMinutes(60));
        }
        return Ok(new { message = "Logged out" });
    }

    // Example protected endpoints:
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("admin-check")]
    public IActionResult AdminCheck() => Ok(new { ok = true, youAre = "Admin" });

    [Authorize(Policy = "TourOperatorOnly")]
    [HttpGet("operator-check")]
    public IActionResult OperatorCheck() =>
        Ok(new { ok = true, youAre = "TourOperator", tourOperatorId = User.FindFirst("tourOperatorId")?.Value });
}

public record LoginRequest(string Username, string Password);
