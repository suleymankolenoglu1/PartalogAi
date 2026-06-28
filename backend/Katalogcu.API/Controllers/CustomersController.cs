using Katalogcu.API.Services;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.Customers.Common;
using Katalogcu.Application.Features.Customers.Commands.ConfirmPasswordReset;
using Katalogcu.Application.Features.Customers.Commands.PublicLogin;
using Katalogcu.Application.Features.Customers.Commands.PublicRegisterAccount;
using Katalogcu.Application.Features.Customers.Commands.RequestPasswordReset;
using Katalogcu.Application.Features.Customers.Commands.SetPortalCustomerAccess;
using Katalogcu.Application.Features.Customers.Commands.UpsertPortalCustomer;
using Katalogcu.Application.Features.Customers.Queries.GetMyCustomers;
using Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerMe;
using Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrderDetail;
using Katalogcu.Application.Features.Customers.Queries.GetPublicCustomerOrders;
using Katalogcu.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Katalogcu.API.Controllers
{
    [Authorize(Policy = "PrivilegedUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly IPublicAccessTokenService _publicAccessTokenService;
        private readonly IPublicCatalogLinkService _publicCatalogLinkService;
        private readonly AppDbContext _dbContext;
        private readonly ISender _sender;

        public CustomersController(
            IPublicAccessTokenService publicAccessTokenService,
            IPublicCatalogLinkService publicCatalogLinkService,
            AppDbContext dbContext,
            ISender sender)
        {
            _publicAccessTokenService = publicAccessTokenService;
            _publicCatalogLinkService = publicCatalogLinkService;
            _dbContext = dbContext;
            _sender = sender;
        }

        private string? ResolvePublicSessionToken()
        {
            var sessionTokenFromHeader = Request.Headers["X-Public-Session"].ToString();
            if (!string.IsNullOrWhiteSpace(sessionTokenFromHeader))
            {
                return sessionTokenFromHeader.Trim();
            }
            return null;
        }

        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idString, out var guid) ? guid : Guid.Empty;
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

        [HttpPost("portal-users")]
        public async Task<IActionResult> CreatePortalUser([FromBody] UpsertPortalCustomerRequest request)
        {
            var ownerUserId = GetCurrentUserId();
            if (ownerUserId == Guid.Empty) return Unauthorized();

            try
            {
                var result = await _sender.Send(new UpsertPortalCustomerCommand(
                    ownerUserId,
                    CustomerId: null,
                    request.Name,
                    request.Phone,
                    request.Email,
                    request.CompanyName,
                    request.Note,
                    request.InitialPassword,
                    request.IsActive));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "conflict" => Conflict(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Portal kullanıcısı oluşturulamadı.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("portal-users/{customerId:guid}")]
        public async Task<IActionResult> UpdatePortalUser(Guid customerId, [FromBody] UpsertPortalCustomerRequest request)
        {
            var ownerUserId = GetCurrentUserId();
            if (ownerUserId == Guid.Empty) return Unauthorized();

            try
            {
                var result = await _sender.Send(new UpsertPortalCustomerCommand(
                    ownerUserId,
                    customerId,
                    request.Name,
                    request.Phone,
                    request.Email,
                    request.CompanyName,
                    request.Note,
                    request.InitialPassword,
                    request.IsActive));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "conflict" => Conflict(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Portal kullanıcısı güncellenemedi.")
                    };
                }

                return Ok(result.Value);
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("portal-users/{customerId:guid}/access")]
        public async Task<IActionResult> SetPortalUserAccess(Guid customerId, [FromBody] SetPortalCustomerAccessRequest request)
        {
            var ownerUserId = GetCurrentUserId();
            if (ownerUserId == Guid.Empty) return Unauthorized();

            try
            {
                var result = await _sender.Send(new SetPortalCustomerAccessCommand(
                    ownerUserId,
                    customerId,
                    request.IsActive));

                if (!result.IsSuccess)
                {
                    return result.ErrorCode switch
                    {
                        "not_found" => NotFound(result.ErrorMessage),
                        "validation" => BadRequest(result.ErrorMessage),
                        _ => StatusCode(500, result.ErrorMessage ?? "Portal erişimi güncellenemedi.")
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
        [HttpPost("public-auth/portal-login")]
        public async Task<IActionResult> PublicPortalLogin([FromBody] PublicPortalLoginRequest request, CancellationToken cancellationToken)
        {
            var identifier = request.Identifier?.Trim() ?? string.Empty;
            var isEmailLogin = identifier.Contains('@');
            var normalizedPhone = isEmailLogin ? string.Empty : NormalizePhone(identifier);
            var normalizedEmail = isEmailLogin ? NormalizeEmail(identifier) : null;

            if (string.IsNullOrWhiteSpace(request.Password) ||
                (string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(normalizedEmail)))
            {
                return BadRequest("Telefon/e-posta ve şifre zorunlu.");
            }

            var candidates = await (
                from portalCustomer in _dbContext.Customers
                join portalOwner in _dbContext.Users on portalCustomer.UserId equals portalOwner.Id
                where portalCustomer.IsActive
                      && portalOwner.PublicLinkEnabled
                      && (
                          (!string.IsNullOrWhiteSpace(normalizedPhone) && portalCustomer.NormalizedPhone == normalizedPhone)
                          || (!string.IsNullOrWhiteSpace(normalizedEmail) && portalCustomer.Email != null && portalCustomer.Email.ToLower() == normalizedEmail)
                      )
                select new { Customer = portalCustomer, Owner = portalOwner })
                .Take(2)
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                return NotFound("Müşteri kaydı bulunamadı.");
            }

            if (candidates.Count > 1)
            {
                return Conflict("Bu telefon/e-posta birden fazla portalda kayıtlı. İşletmenizin gönderdiği portal bağlantısıyla giriş yapın.");
            }

            var match = candidates[0];
            var customer = match.Customer;
            var owner = match.Owner;

            if (customer.LoginLockoutUntil != null && customer.LoginLockoutUntil > DateTime.UtcNow)
            {
                var remainingMinutes = Math.Ceiling((customer.LoginLockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
                return StatusCode(429, $"Çok fazla hatalı giriş denemesi. Lütfen {remainingMinutes:0} dakika sonra tekrar deneyin.");
            }

            if (string.IsNullOrWhiteSpace(customer.PasswordHash) || string.IsNullOrWhiteSpace(customer.PasswordSalt))
            {
                return BadRequest("Bu müşteri için hesap şifresi tanımlı değil.");
            }

            if (!VerifyPassword(request.Password, customer.PasswordHash, customer.PasswordSalt))
            {
                customer.FailedLoginCount += 1;
                customer.UpdatedDate = DateTime.UtcNow;
                if (customer.FailedLoginCount >= 5)
                {
                    customer.LoginLockoutUntil = DateTime.UtcNow.AddMinutes(15);
                    customer.FailedLoginCount = 0;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return Unauthorized("Telefon/e-posta veya şifre hatalı.");
            }

            customer.LastVisitDate = DateTime.UtcNow;
            customer.FailedLoginCount = 0;
            customer.LoginLockoutUntil = null;
            customer.PublicSessionToken = CreateSessionToken();
            customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(30);
            customer.LastLoginDate = DateTime.UtcNow;
            customer.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var publicToken = _publicCatalogLinkService.CreateToken(owner.Id, owner.PublicLinkVersion);
            return Ok(new PortalHomeLoginResponse
            {
                Success = true,
                PublicToken = publicToken,
                SessionToken = customer.PublicSessionToken ?? string.Empty,
                Customer = new PublicCustomerDto
                {
                    Id = customer.Id,
                    Name = customer.FullName,
                    Phone = customer.Phone,
                    Email = customer.Email,
                    Company = customer.CompanyName
                }
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-register")]
        public IActionResult PublicRegister()
        {
            return StatusCode(403, new
            {
                message = "Müşteri kaydı public link ile açılamaz. Portal erişimi panelden tanımlanan müşterilerle sınırlıdır."
            });
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
                        "not_found" => NotFound(result.ErrorMessage),
                        "inactive" => StatusCode(403, result.ErrorMessage),
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
                        "inactive" => StatusCode(403, result.ErrorMessage),
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
        public async Task<IActionResult> GetPublicCustomerMe([FromQuery] string publicToken)
        {
            var payload = _publicAccessTokenService.Validate(publicToken);
            if (payload == null) return Unauthorized("Oturum geçersiz.");

            var resolvedSessionToken = ResolvePublicSessionToken();
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
        public async Task<IActionResult> GetPublicCustomerOrders([FromQuery] string publicToken)
        {
            var payload = _publicAccessTokenService.Validate(publicToken);
            if (payload == null) return Unauthorized("Oturum geçersiz.");

            var resolvedSessionToken = ResolvePublicSessionToken();
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
            [FromQuery] string publicToken)
        {
            var payload = _publicAccessTokenService.Validate(publicToken);
            if (payload == null) return Unauthorized("Oturum geçersiz.");

            var resolvedSessionToken = ResolvePublicSessionToken();
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

        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            return new string(phone.Where(char.IsDigit).ToArray());
        }

        private static string? NormalizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return email.Trim().ToLowerInvariant();
        }

        private static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
                return false;

            byte[] saltBytes;
            byte[] expectedHashBytes;
            try
            {
                saltBytes = Convert.FromBase64String(storedSalt);
                expectedHashBytes = Convert.FromBase64String(storedHash);
            }
            catch
            {
                return false;
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 120_000, HashAlgorithmName.SHA256);
            var actualHashBytes = pbkdf2.GetBytes(32);
            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }

        private static string CreateSessionToken()
        {
            return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        }
    }

    public sealed class UpsertPortalCustomerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? CompanyName { get; set; }
        public string? Note { get; set; }
        public string? InitialPassword { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class SetPortalCustomerAccessRequest
    {
        public bool IsActive { get; set; }
    }

    public class PublicCustomerLoginRequest
    {
        public string PublicToken { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class PublicPortalLoginRequest
    {
        public string Identifier { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class PortalHomeLoginResponse
    {
        public bool Success { get; init; }
        public string PublicToken { get; init; } = string.Empty;
        public string SessionToken { get; init; } = string.Empty;
        public PublicCustomerDto Customer { get; init; } = new();
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
