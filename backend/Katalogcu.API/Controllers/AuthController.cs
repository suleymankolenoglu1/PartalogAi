using FluentValidation;
using Katalogcu.API.Services;
using Katalogcu.Application.Features.Auth.Commands.CancelPlan;
using Katalogcu.Application.Features.Auth.Commands.Login;
using Katalogcu.Application.Features.Auth.Commands.Register;
using Katalogcu.Application.Features.Auth.Commands.SelectPlan;
using Katalogcu.Application.Features.Auth.Commands.UpdateMe;
using Katalogcu.Application.Features.Auth.Queries.GetMe;
using Katalogcu.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
        public sealed class SelectPlanRequest
        {
            public int Plan { get; set; }
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth-login")]
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
                        "password_upgrade_required" => StatusCode(403, new { message = result.ErrorMessage }),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Giriş işlemi başarısız.")
                    };
                }

                var response = result.Value!;
                return Ok(new
                {
                    token = response.Token,
                    userId = response.User.UserId,
                    plan = response.User.SubscriptionPlan,
                    planName = GetPlanName(response.User.SubscriptionPlan),
                    planSelected = response.User.PlanSelected,
                    maxCatalogs = GetPlanMaxCatalogCount(response.User.SubscriptionPlan),
                    expiresAt = response.User.PlanExpiresAt,
                    user = new
                    {
                        id = response.User.Id,
                        userId = response.User.UserId,
                        firstName = response.User.FirstName,
                        lastName = response.User.LastName,
                        email = response.User.Email,
                        companyName = response.User.CompanyName,
                        phoneNumber = response.User.PhoneNumber,
                        role = response.User.Role,
                        subscriptionPlan = response.User.SubscriptionPlan,
                        planActivatedAt = response.User.PlanActivatedAt,
                        planExpiresAt = response.User.PlanExpiresAt,
                        planSelected = response.User.PlanSelected,
                        maxCatalogCount = GetPlanMaxCatalogCount(response.User.SubscriptionPlan),
                        maxPagePerCatalog = response.User.MaxPagePerCatalog
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
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    companyName = user.CompanyName,
                    phoneNumber = user.PhoneNumber,
                    role = user.Role,
                    subscriptionPlan = user.SubscriptionPlan,
                    planName = GetPlanName(user.SubscriptionPlan),
                    planActivatedAt = user.PlanActivatedAt,
                    planExpiresAt = user.PlanExpiresAt,
                    planSelected = user.PlanSelected,
                    maxCatalogCount = GetPlanMaxCatalogCount(user.SubscriptionPlan),
                    maxPagePerCatalog = user.MaxPagePerCatalog
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
                        "forbidden" => StatusCode(StatusCodes.Status403Forbidden, result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        "not_found" => NotFound(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Profil güncellenemedi.")
                    };
                }

                var user = result.Value!;
                return Ok(new
                {
                    id = user.Id,
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    companyName = user.CompanyName,
                    phoneNumber = user.PhoneNumber,
                    role = user.Role,
                    subscriptionPlan = user.SubscriptionPlan,
                    planName = GetPlanName(user.SubscriptionPlan),
                    planActivatedAt = user.PlanActivatedAt,
                    planExpiresAt = user.PlanExpiresAt,
                    planSelected = user.PlanSelected,
                    maxCatalogCount = GetPlanMaxCatalogCount(user.SubscriptionPlan),
                    maxPagePerCatalog = user.MaxPagePerCatalog
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost("select-plan")]
        public async Task<IActionResult> SelectPlan([FromBody] SelectPlanRequest request)
        {
            try
            {
                var result = await _sender.Send(new SelectPlanCommand(request.Plan));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(),
                        "validation" => BadRequest(result.ErrorMessage),
                        "not_found" => NotFound(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Plan güncellenemedi.")
                    };
                }

                var user = result.Value!;
                return Ok(new
                {
                    id = user.Id,
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    companyName = user.CompanyName,
                    phoneNumber = user.PhoneNumber,
                    role = user.Role,
                    subscriptionPlan = user.SubscriptionPlan,
                    planName = GetPlanName(user.SubscriptionPlan),
                    planActivatedAt = user.PlanActivatedAt,
                    planExpiresAt = user.PlanExpiresAt,
                    planSelected = user.PlanSelected,
                    maxCatalogCount = GetPlanMaxCatalogCount(user.SubscriptionPlan),
                    maxPagePerCatalog = user.MaxPagePerCatalog
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpPost("cancel-plan")]
        public async Task<IActionResult> CancelPlan()
        {
            try
            {
                var result = await _sender.Send(new CancelPlanCommand());
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(),
                        "validation" => BadRequest(result.ErrorMessage),
                        "not_found" => NotFound(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Plan iptali başarısız.")
                    };
                }

                var user = result.Value!;
                return Ok(new
                {
                    id = user.Id,
                    userId = user.UserId,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    companyName = user.CompanyName,
                    phoneNumber = user.PhoneNumber,
                    role = user.Role,
                    subscriptionPlan = user.SubscriptionPlan,
                    planName = GetPlanName(user.SubscriptionPlan),
                    planActivatedAt = user.PlanActivatedAt,
                    planExpiresAt = user.PlanExpiresAt,
                    planSelected = user.PlanSelected,
                    maxCatalogCount = GetPlanMaxCatalogCount(user.SubscriptionPlan),
                    maxPagePerCatalog = user.MaxPagePerCatalog
                });
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private static string GetPlanName(int plan)
        {
            return (SubscriptionPlan)plan switch
            {
                SubscriptionPlan.CatalogOnly => "Catalog",
                SubscriptionPlan.CatalogWithAI => "CatalogWithAI",
                SubscriptionPlan.CatalogWithAIAndEcommerce => "CatalogWithAIAndEcommerce",
                _ => "Catalog"
            };
        }

        private static int GetPlanMaxCatalogCount(int plan)
        {
            var limits = PlanLimitRules.For((SubscriptionPlan)plan);
            return limits.MaxCatalogCount ?? int.MaxValue;
        }
    }
}
