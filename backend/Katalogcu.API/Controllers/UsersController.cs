using FluentValidation;
using Katalogcu.Application.Features.Users.Commands.CreateUser;
using Katalogcu.Application.Features.Users.Queries.GetAllUsers;
using Katalogcu.Application.Features.Users.Queries.GetUserById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ISender _sender;

        public UsersController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _sender.Send(new GetAllUsersQuery());
            if (!result.IsSuccess)
            {
                return StatusCode(500, result.ErrorMessage ?? "Kullanıcılar alınamadı.");
            }

            return Ok(result.Value);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _sender.Send(new GetUserByIdQuery(id));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Kullanıcı alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            try
            {
                var password = string.IsNullOrWhiteSpace(request.PasswordHash)
                    ? request.Password ?? string.Empty
                    : request.PasswordHash;

                var result = await _sender.Send(new CreateUserCommand(
                    request.FirstName ?? string.Empty,
                    request.LastName ?? string.Empty,
                    request.Email ?? string.Empty,
                    password,
                    request.Role ?? "Owner",
                    request.CompanyName,
                    request.PhoneNumber));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Kullanıcı oluşturulamadı.")
                    };
                }

                var created = result.Value!;
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public sealed class CreateUserRequest
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Password { get; set; }
            public string? PasswordHash { get; set; }
            public string? Role { get; set; }
            public string? CompanyName { get; set; }
            public string? PhoneNumber { get; set; }
        }
    }
}
