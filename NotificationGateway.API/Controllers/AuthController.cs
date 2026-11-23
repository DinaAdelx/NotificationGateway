using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationGateway.API.Authentication;
using NotificationGateway.Application.Users.LoginUser;
using NotificationGateway.Application.Users.RegisterUser;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IMediator mediator,
    NotificationContext context) : ControllerBase
{
    public record RegisterRequest(
        string Email,
        string Password,
        string FullName,
        string Role = "User");

    public record LoginRequest(
        string Email,
        string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (context.TenantId == Guid.Empty || context.ApplicationId == Guid.Empty)
            return Unauthorized(new { message = "Valid tenant and application context are required (via X-Tenant-Id / X-Application-Id headers or claims)." });

        try
        {
            var cmd = new RegisterUserCommand(
                context.TenantId,
                context.ApplicationId,
                request.Email,
                request.Password,
                request.FullName,
                request.Role);

            var result = await mediator.Send(cmd, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (context.TenantId == Guid.Empty || context.ApplicationId == Guid.Empty)
            return Unauthorized(new { message = "Valid tenant and application context are required (via X-Tenant-Id / X-Application-Id headers or claims)." });

        try
        {
            var cmd = new LoginUserCommand(
                context.TenantId,
                context.ApplicationId,
                request.Email,
                request.Password);

            var result = await mediator.Send(cmd, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}
