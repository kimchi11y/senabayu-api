using Homestay.Api.Dtos;
using Homestay.Api.Infrastructure;
using Homestay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homestay.Api.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
public class AdminAuthController(AdminAuthService auth, JwtTokens tokens) : ControllerBase
{
    /// <summary>Exchanges an admin's email and password for a one-hour access token.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.AdminLogin)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var admin = await auth.ValidateAsync(request.Email, request.Password, cancellationToken)
            ?? throw new BookingException(
                StatusCodes.Status401Unauthorized, "invalid_credentials", "Wrong email or password.");

        var token = tokens.Issue(admin);
        return new LoginResponse(token.Token, token.ExpiresAt);
    }
}
