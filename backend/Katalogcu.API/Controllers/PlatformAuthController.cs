using FluentValidation;
using Katalogcu.Application.Features.Auth.Commands.Login;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Katalogcu.API.Controllers;

[Route("api/platform-auth")]
[ApiController]
public sealed class PlatformAuthController : ControllerBase
{
    private readonly ISender _sender;

    public PlatformAuthController(ISender sender)
    {
        _sender = sender;
    }

    public sealed record PlatformLoginRequest(string Email, string Password);

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login([FromBody] PlatformLoginRequest request)
    {
        try
        {
            var result = await _sender.Send(new LoginCommand(request.Email, request.Password));
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "unauthorized" => Unauthorized(new { message = result.ErrorMessage }),
                    "password_upgrade_required" => StatusCode(403, new { message = result.ErrorMessage }),
                    "validation" => BadRequest(result.ErrorMessage),
                    _ => StatusCode(500, result.ErrorMessage ?? "Giriş işlemi başarısız.")
                };
            }

            var response = result.Value!;
            if (!string.Equals(response.User.Role, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            return Ok(new
            {
                token = response.Token,
                userId = response.User.UserId,
                role = response.User.Role,
                user = new
                {
                    id = response.User.Id,
                    userId = response.User.UserId,
                    firstName = response.User.FirstName,
                    lastName = response.User.LastName,
                    email = response.User.Email,
                    role = response.User.Role
                }
            });
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
