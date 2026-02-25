using FluentValidation;
using Katalogcu.Application.Features.Auth.Commands.Login;
using Katalogcu.Application.Features.Auth.Commands.Register;
using Katalogcu.Application.Features.Auth.Commands.UpdateMe;
using Katalogcu.Application.Features.Auth.Queries.GetMe;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ISender _sender;

        public AuthController(ISender sender)
        {
            _sender = sender;
        }

        public record LoginRequest(string Email, string Password);
        public record RegisterRequest(string FullName, string Email, string Password);
        public sealed class UpdateMeRequest
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string? CompanyName { get; set; }
            public string? PhoneNumber { get; set; }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _sender.Send(new LoginCommand(request.Email, request.Password));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(new { message = result.ErrorMessage }),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Giriş işlemi başarısız.")
                    };
                }

                var response = result.Value!;
                return Ok(new
                {
                    token = response.Token,
                    user = new
                    {
                        id = response.User.Id,
                        userId = response.User.UserId,
                        firstName = response.User.FirstName,
                        lastName = response.User.LastName,
                        email = response.User.Email,
                        companyName = response.User.CompanyName,
                        phoneNumber = response.User.PhoneNumber,
                        role = response.User.Role
                    }
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var result = await _sender.Send(new RegisterCommand(request.FullName, request.Email, request.Password));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "duplicate" => BadRequest(new { message = result.ErrorMessage }),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Kayıt işlemi başarısız.")
                    };
                }

                return Ok(new { message = result.Value!.Message });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var result = await _sender.Send(new GetMeQuery());
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(),
                        "not_found" => NotFound(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Kullanıcı bilgisi alınamadı.")
                    };
                }

                var user = result.Value!;
                return Ok(new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    companyName = user.CompanyName,
                    phoneNumber = user.PhoneNumber,
                    role = user.Role
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
        {
            try
            {
                var result = await _sender.Send(new UpdateMeCommand(
                    request.FirstName,
                    request.LastName,
                    request.CompanyName,
                    request.PhoneNumber));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(),
                        "validation" => BadRequest(result.ErrorMessage),
                        "not_found" => NotFound(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Profil güncellenemedi.")
                    };
                }

                var user = result.Value!;
                return Ok(new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    companyName = user.CompanyName,
                    phoneNumber = user.PhoneNumber,
                    role = user.Role
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
