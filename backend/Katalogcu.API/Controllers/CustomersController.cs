using Katalogcu.API.Services;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Customers.Commands.ConfirmPasswordReset;
using Katalogcu.Application.Features.Customers.Commands.PublicLogin;
using Katalogcu.Application.Features.Customers.Commands.PublicRegister;
using Katalogcu.Application.Features.Customers.Commands.PublicRegisterAccount;
using Katalogcu.Application.Features.Customers.Commands.RequestPasswordReset;
using Katalogcu.Application.Features.Customers.Queries.GetMyCustomers;
using Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerMe;
using Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrderDetail;
using Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrders;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly IPublicAccessTokenService _publicAccessTokenService;
        private readonly ISender _sender;

        public CustomersController(IPublicAccessTokenService publicAccessTokenService, ISender sender)
        {
            _publicAccessTokenService = publicAccessTokenService;
            _sender = sender;
        }

        private string? ResolvePublicSessionToken(string? sessionTokenFromQuery)
        {
            var sessionTokenFromHeader = Request.Headers["X-Public-Session"].ToString();
            if (!string.IsNullOrWhiteSpace(sessionTokenFromHeader))
            {
                return sessionTokenFromHeader.Trim();
            }

            return string.IsNullOrWhiteSpace(sessionTokenFromQuery) ? null : sessionTokenFromQuery.Trim();
        }

        [HttpGet]
        public async Task<IActionResult> GetMyCustomers()
        {
            try
            {
                var result = await _sender.Send(new GetMyCustomersQuery());
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Müşteriler alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-register")]
        public async Task<IActionResult> PublicRegister([FromBody] PublicCustomerRegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicAccessTokenService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            try
            {
                var result = await _sender.Send(new PublicRegisterCustomerCommand(
                    payload.UserId,
                    request.Name,
                    request.Phone,
                    request.Email,
                    request.CompanyName,
                    request.Note));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Müşteri kaydı oluşturulamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/register")]
        public async Task<IActionResult> PublicRegisterAccount([FromBody] PublicCustomerRegisterAccountRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicAccessTokenService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            try
            {
                var result = await _sender.Send(new PublicRegisterCustomerAccountCommand(
                    payload.UserId,
                    request.Name,
                    request.Phone,
                    request.Email,
                    request.Password));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "conflict" => Conflict(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Hesap oluşturulamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/login")]
        public async Task<IActionResult> PublicLogin([FromBody] PublicCustomerLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicAccessTokenService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            try
            {
                var result = await _sender.Send(new PublicCustomerLoginCommand(
                    payload.UserId,
                    request.Phone,
                    request.Email,
                    request.Password));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "locked" => StatusCode(429, result.ErrorMessage),
                        "no_password" => BadRequest(result.ErrorMessage),
                        "invalid_credentials" => Unauthorized(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Giriş yapılamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/password-reset/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PublicCustomerPasswordResetRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicAccessTokenService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            try
            {
                var result = await _sender.Send(new RequestCustomerPasswordResetCommand(
                    payload.UserId,
                    request.Phone,
                    request.Email));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Şifre sıfırlama kodu oluşturulamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/password-reset/confirm")]
        public async Task<IActionResult> ConfirmPasswordReset([FromBody] PublicCustomerPasswordResetConfirmRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicAccessTokenService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            try
            {
                var result = await _sender.Send(new ConfirmCustomerPasswordResetCommand(
                    payload.UserId,
                    request.Phone,
                    request.Email,
                    request.ResetCode,
                    request.NewPassword));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Şifre sıfırlanamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet("public-auth/me")]
        public async Task<IActionResult> GetPublicCustomerMe([FromQuery] string publicToken, [FromQuery] string? sessionToken = null)
        {
            var payload = _publicAccessTokenService.Validate(publicToken);
            if (payload == null) return Unauthorized("Oturum geçersiz.");

            var resolvedSessionToken = ResolvePublicSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(resolvedSessionToken)) return Unauthorized("Oturum geçersiz.");

            try
            {
                var result = await _sender.Send(new GetPublicCustomerMeQuery(payload.UserId, resolvedSessionToken));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Müşteri bilgisi alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet("public-auth/orders")]
        public async Task<IActionResult> GetPublicCustomerOrders([FromQuery] string publicToken, [FromQuery] string? sessionToken = null)
        {
            var payload = _publicAccessTokenService.Validate(publicToken);
            if (payload == null) return Unauthorized("Oturum geçersiz.");

            var resolvedSessionToken = ResolvePublicSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(resolvedSessionToken)) return Unauthorized("Oturum geçersiz.");

            try
            {
                var result = await _sender.Send(new GetPublicCustomerOrdersQuery(payload.UserId, resolvedSessionToken));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Siparişler alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet("public-auth/orders/{orderId:guid}")]
        public async Task<IActionResult> GetPublicCustomerOrderDetail(
            Guid orderId,
            [FromQuery] string publicToken,
            [FromQuery] string? sessionToken = null)
        {
            var payload = _publicAccessTokenService.Validate(publicToken);
            if (payload == null) return Unauthorized("Oturum geçersiz.");

            var resolvedSessionToken = ResolvePublicSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(resolvedSessionToken)) return Unauthorized("Oturum geçersiz.");

            try
            {
                var result = await _sender.Send(new GetPublicCustomerOrderDetailQuery(payload.UserId, resolvedSessionToken, orderId));
                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "unauthorized" => Unauthorized(result.ErrorMessage),
                        "not_found" => NotFound(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Sipariş detayı alınamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public class PublicCustomerRegisterRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? CompanyName { get; set; }
        public string? Note { get; set; }
    }

    public class PublicCustomerLoginRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class PublicCustomerRegisterAccountRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class PublicCustomerPasswordResetRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class PublicCustomerPasswordResetConfirmRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string ResetCode { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
