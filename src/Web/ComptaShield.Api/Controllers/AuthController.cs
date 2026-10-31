using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ComptaShield.Api.Controllers;

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string Token, Guid UserId, Guid CabinetId, string Email, string Role);

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // For development/demo purposes: issue JWT with cabinet_id claim
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cabinetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var jwtKey = _config["Jwt:Key"] ?? "ComptaShield_SuperSecret_Jwt_SigningKey_2026_AtLeast32Bytes!";
        var jwtIssuer = _config["Jwt:Issuer"] ?? "ComptaShield.Identity";
        var jwtAudience = _config["Jwt:Audience"] ?? "ComptaShield.Api";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim("cabinet_id", cabinetId.ToString()),
            new Claim(ClaimTypes.Email, request.Email),
            new Claim(ClaimTypes.Role, "Partner")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new LoginResponse(tokenString, userId, cabinetId, request.Email, "Partner"));
    }
}
