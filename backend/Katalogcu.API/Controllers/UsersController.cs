using FluentValidation;
using Katalogcu.Application.Features.Users.Commands.CreateUser;
using Katalogcu.Application.Features.Users.Queries.GetAllUsers;
using Katalogcu.Application.Features.Users.Queries.GetUserById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Katalogcu.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ISender _sender;

        public UsersController(ISender sender)
        {
            _sender = sender;
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _sender.Send(new GetAllUsersQuery());
            if (!result.IsSuccess)
            {
                return StatusCode(500, result.ErrorMessage ?? "Kullanıcılar alınamadı.");
            }

            return Ok(result.Value!.Select(MapUser).ToList());
        }

        [Authorize(Policy = "AdminOnly")]
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

                return Ok(MapUser(result.Value!));
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Policy = "PrivilegedUser")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            try
            {
                var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                var requestedRole = request.Role ?? "Customer";

                if (!string.Equals(requesterRole, "admin", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(requestedRole, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                var password = request.Password ?? string.Empty;

                var result = await _sender.Send(new CreateUserCommand(
                    request.FirstName ?? string.Empty,
                    request.LastName ?? string.Empty,
                    request.Email ?? string.Empty,
                    password,
                    request.Role ?? "Customer",
                    request.CompanyName,
                    request.PhoneNumber));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        "duplicate" => BadRequest(new { message = result.ErrorMessage }),
                        _ => StatusCode(500, result.ErrorMessage ?? "Kullanıcı oluşturulamadı.")
                    };
                }

                var created = result.Value!;
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapUser(created));
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
            public string? Role { get; set; }
            public string? CompanyName { get; set; }
            public string? PhoneNumber { get; set; }
        }

        private static object MapUser(Katalogcu.Domain.Entities.AppUser user)
        {
            return new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role,
                user.CompanyName,
                user.PhoneNumber,
                user.SubscriptionPlan,
                user.PlanActivatedAt,
                user.PlanExpiresAt,
                user.MaxCatalogCount,
                user.MaxPagePerCatalog,
                user.CreatedDate,
                user.UpdatedDate
            };
        }
    }
}
