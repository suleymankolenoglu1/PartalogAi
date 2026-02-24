using System.Security.Claims;
using System.Security.Cryptography;
using Katalogcu.API.Services;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPublicLinkService _publicLinkService;
        private const int MaxFailedLoginAttempts = 5;
        private static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResetCodeDuration = TimeSpan.FromMinutes(10);

        public CustomersController(AppDbContext context, IPublicLinkService publicLinkService)
        {
            _context = context;
            _publicLinkService = publicLinkService;
        }

        private Guid GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idString, out var guid)) return guid;
            return Guid.Empty;
        }

        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            return new string(phone.Where(char.IsDigit).ToArray());
        }

        private async Task<Customer?> ValidatePublicSessionAsync(string publicToken, string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(publicToken) || string.IsNullOrWhiteSpace(sessionToken))
                return null;

            var payload = _publicLinkService.Validate(publicToken);
            if (payload == null) return null;

            var now = DateTime.UtcNow;
            var customer = await _context.Customers.FirstOrDefaultAsync(c =>
                c.UserId == payload.UserId &&
                c.PublicSessionToken == sessionToken &&
                c.PublicSessionExpiresAt != null &&
                c.PublicSessionExpiresAt > now);

            return customer;
        }

        private static string? NormalizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return email.Trim().ToLowerInvariant();
        }

        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            return string.Join(" ", name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool IsPasswordStrong(string password) => !string.IsNullOrWhiteSpace(password) && password.Length >= 8;

        private static void CreatePasswordHash(string password, out string hash, out string salt)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(16);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 120_000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32);
            hash = Convert.ToBase64String(hashBytes);
            salt = Convert.ToBase64String(saltBytes);
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

        private static string GenerateResetCode()
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return code.ToString("D6");
        }

        private static bool IsDebugEnabled()
        {
            var raw = Environment.GetEnvironmentVariable("DEBUG");
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return raw.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   raw.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<Customer?> FindCustomerByPhoneOrEmailAsync(Guid userId, string normalizedPhone, string? normalizedEmail)
        {
            Customer? customer = null;

            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c =>
                    c.UserId == userId &&
                    c.NormalizedPhone == normalizedPhone);
            }

            if (customer == null && !string.IsNullOrWhiteSpace(normalizedEmail))
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c =>
                    c.UserId == userId &&
                    c.Email != null &&
                    c.Email.ToLower() == normalizedEmail);
            }

            return customer;
        }

        private string CreateSessionToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        [HttpGet]
        public async Task<IActionResult> GetMyCustomers()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized("Geçersiz kullanıcı.");

            var customers = await _context.Customers
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastVisitDate)
                .Select(c => new
                {
                    c.Id,
                    name = c.FullName,
                    company = c.CompanyName,
                    email = c.Email,
                    phone = c.Phone,
                    orderCount = c.OrderCount,
                    totalSpent = c.TotalSpent,
                    lastVisitDate = c.LastVisitDate,
                    lastOrderDate = c.LastOrderDate,
                    status = c.IsActive ? "active" : "inactive",
                    note = c.Note,
                    createdDate = c.CreatedDate
                })
                .ToListAsync();

            return Ok(customers);
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-register")]
        public async Task<IActionResult> PublicRegister([FromBody] PublicCustomerRegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
                return BadRequest("Ad soyad ve telefon zorunludur.");

            var payload = _publicLinkService.Validate(request.PublicToken);
            if (payload == null)
                return BadRequest("Geçersiz public link.");

            var normalizedPhone = NormalizePhone(request.Phone);
            var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : request.Email.Trim().ToLowerInvariant();

            Customer? customer = null;

            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c =>
                    c.UserId == payload.UserId &&
                    c.NormalizedPhone == normalizedPhone);
            }

            if (customer == null && !string.IsNullOrWhiteSpace(normalizedEmail))
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c =>
                    c.UserId == payload.UserId &&
                    c.Email != null &&
                    c.Email.ToLower() == normalizedEmail);
            }

            var isNew = customer == null;
            if (isNew)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    UserId = payload.UserId,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Customers.Add(customer);
            }

            customer!.FullName = request.Name.Trim();
            customer.Phone = request.Phone.Trim();
            customer.NormalizedPhone = string.IsNullOrWhiteSpace(normalizedPhone) ? customer.NormalizedPhone : normalizedPhone;
            customer.Email = string.IsNullOrWhiteSpace(normalizedEmail) ? customer.Email : normalizedEmail;
            customer.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? customer.CompanyName : request.CompanyName.Trim();
            customer.Note = string.IsNullOrWhiteSpace(request.Note) ? customer.Note : request.Note.Trim();
            customer.LastVisitDate = DateTime.UtcNow;
            customer.IsActive = true;
            customer.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                created = isNew,
                customerId = customer.Id,
                message = isNew ? "Müşteri kaydı oluşturuldu." : "Müşteri bilgisi güncellendi."
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/register")]
        public async Task<IActionResult> PublicRegisterAccount([FromBody] PublicCustomerRegisterAccountRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicLinkService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            var normalizedPhone = NormalizePhone(request.Phone);
            var normalizedEmail = NormalizeEmail(request.Email);
            if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Ad soyad ve telefon zorunludur.");
            if (!IsPasswordStrong(request.Password))
                return BadRequest("Şifre en az 8 karakter olmalıdır.");

            var existing = await FindCustomerByPhoneOrEmailAsync(payload.UserId, normalizedPhone, normalizedEmail);
            if (existing != null && !string.IsNullOrWhiteSpace(existing.PasswordHash) && !string.IsNullOrWhiteSpace(existing.PasswordSalt))
            {
                return Conflict("Bu telefon/e-posta ile kayıtlı hesap zaten var. Lütfen giriş yapın.");
            }

            var customer = existing ?? new Customer
            {
                Id = Guid.NewGuid(),
                UserId = payload.UserId,
                CreatedDate = DateTime.UtcNow
            };

            customer.FullName = request.Name.Trim();
            customer.Phone = request.Phone.Trim();
            customer.NormalizedPhone = normalizedPhone;
            customer.Email = string.IsNullOrWhiteSpace(normalizedEmail) ? customer.Email : normalizedEmail;
            customer.LastVisitDate = DateTime.UtcNow;
            customer.LastLoginDate = DateTime.UtcNow;
            customer.IsActive = true;
            customer.UpdatedDate = DateTime.UtcNow;

            CreatePasswordHash(request.Password, out var hash, out var salt);
            customer.PasswordHash = hash;
            customer.PasswordSalt = salt;
            customer.FailedLoginCount = 0;
            customer.LoginLockoutUntil = null;
            customer.LoginCode = null;
            customer.LoginCodeExpiresAt = null;

            customer.PublicSessionToken = CreateSessionToken();
            customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(30);

            if (existing == null) _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                sessionToken = customer.PublicSessionToken,
                customer = new
                {
                    id = customer.Id,
                    name = customer.FullName,
                    phone = customer.Phone,
                    email = customer.Email,
                    company = customer.CompanyName
                }
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/login")]
        public async Task<IActionResult> PublicLogin([FromBody] PublicCustomerLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicLinkService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            var normalizedPhone = NormalizePhone(request.Phone);
            var normalizedEmail = NormalizeEmail(request.Email);
            if (string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(normalizedEmail))
                return BadRequest("Telefon veya e-posta zorunludur.");
            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Şifre zorunludur.");

            var customer = await FindCustomerByPhoneOrEmailAsync(payload.UserId, normalizedPhone, normalizedEmail);

            if (customer == null)
            {
                return NotFound("Müşteri kaydı bulunamadı. Önce kayıt olmanız gerekiyor.");
            }

            if (customer.LoginLockoutUntil != null && customer.LoginLockoutUntil > DateTime.UtcNow)
            {
                var remainingMinutes = Math.Ceiling((customer.LoginLockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
                return StatusCode(429, $"Çok fazla hatalı giriş denemesi. Lütfen {remainingMinutes:0} dakika sonra tekrar deneyin.");
            }

            if (string.IsNullOrWhiteSpace(customer.PasswordHash) || string.IsNullOrWhiteSpace(customer.PasswordSalt))
                return BadRequest("Bu müşteri için hesap şifresi tanımlı değil. Lütfen kayıt olun.");

            if (!VerifyPassword(request.Password, customer.PasswordHash, customer.PasswordSalt))
            {
                customer.FailedLoginCount += 1;
                customer.UpdatedDate = DateTime.UtcNow;

                if (customer.FailedLoginCount >= MaxFailedLoginAttempts)
                {
                    customer.LoginLockoutUntil = DateTime.UtcNow.Add(LoginLockoutDuration);
                    customer.FailedLoginCount = 0;
                }

                await _context.SaveChangesAsync();
                return Unauthorized("Telefon/e-posta veya şifre hatalı.");
            }

            customer.LastVisitDate = DateTime.UtcNow;
            customer.IsActive = true;
            customer.FailedLoginCount = 0;
            customer.LoginLockoutUntil = null;

            var sessionToken = CreateSessionToken();
            customer.PublicSessionToken = sessionToken;
            customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(30);
            customer.LastLoginDate = DateTime.UtcNow;
            customer.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                sessionToken,
                customer = new
                {
                    id = customer.Id,
                    name = customer.FullName,
                    phone = customer.Phone,
                    email = customer.Email,
                    company = customer.CompanyName
                }
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/password-reset/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PublicCustomerPasswordResetRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicLinkService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            var normalizedPhone = NormalizePhone(request.Phone);
            var normalizedEmail = NormalizeEmail(request.Email);
            if (string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(normalizedEmail))
                return BadRequest("Telefon veya e-posta zorunludur.");

            var customer = await FindCustomerByPhoneOrEmailAsync(payload.UserId, normalizedPhone, normalizedEmail);

            if (customer != null)
            {
                var resetCode = GenerateResetCode();
                customer.LoginCode = resetCode;
                customer.LoginCodeExpiresAt = DateTime.UtcNow.Add(ResetCodeDuration);
                customer.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Şifre sıfırlama kodu oluşturuldu.",
                    resetCode = IsDebugEnabled() ? resetCode : null
                });
            }

            // Kullanıcı enumerasyonunu engellemek için yine başarı döndür.
            return Ok(new
            {
                success = true,
                message = "Şifre sıfırlama kodu oluşturuldu."
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting("public-feedback")]
        [HttpPost("public-auth/password-reset/confirm")]
        public async Task<IActionResult> ConfirmPasswordReset([FromBody] PublicCustomerPasswordResetConfirmRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicToken))
                return BadRequest("Public token zorunludur.");

            var payload = _publicLinkService.Validate(request.PublicToken);
            if (payload == null) return BadRequest("Geçersiz public link.");

            var normalizedPhone = NormalizePhone(request.Phone);
            var normalizedEmail = NormalizeEmail(request.Email);
            if (string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(normalizedEmail))
                return BadRequest("Telefon veya e-posta zorunludur.");
            if (string.IsNullOrWhiteSpace(request.ResetCode))
                return BadRequest("Doğrulama kodu zorunludur.");
            if (!IsPasswordStrong(request.NewPassword))
                return BadRequest("Yeni şifre en az 8 karakter olmalıdır.");

            var customer = await FindCustomerByPhoneOrEmailAsync(payload.UserId, normalizedPhone, normalizedEmail);
            if (customer == null) return BadRequest("Sıfırlama doğrulanamadı.");
            if (string.IsNullOrWhiteSpace(customer.LoginCode) || customer.LoginCodeExpiresAt == null || customer.LoginCodeExpiresAt <= DateTime.UtcNow)
                return BadRequest("Doğrulama kodu geçersiz veya süresi dolmuş.");
            if (!string.Equals(customer.LoginCode, request.ResetCode.Trim(), StringComparison.Ordinal))
                return BadRequest("Doğrulama kodu hatalı.");

            CreatePasswordHash(request.NewPassword, out var hash, out var salt);
            customer.PasswordHash = hash;
            customer.PasswordSalt = salt;
            customer.LoginCode = null;
            customer.LoginCodeExpiresAt = null;
            customer.FailedLoginCount = 0;
            customer.LoginLockoutUntil = null;
            customer.LastLoginDate = DateTime.UtcNow;
            customer.LastVisitDate = DateTime.UtcNow;
            customer.IsActive = true;
            customer.PublicSessionToken = CreateSessionToken();
            customer.PublicSessionExpiresAt = DateTime.UtcNow.AddDays(30);
            customer.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                sessionToken = customer.PublicSessionToken,
                customer = new
                {
                    id = customer.Id,
                    name = customer.FullName,
                    phone = customer.Phone,
                    email = customer.Email,
                    company = customer.CompanyName
                }
            });
        }

        [AllowAnonymous]
        [HttpGet("public-auth/me")]
        public async Task<IActionResult> GetPublicCustomerMe([FromQuery] string publicToken, [FromQuery] string sessionToken)
        {
            var customer = await ValidatePublicSessionAsync(publicToken, sessionToken);
            if (customer == null) return Unauthorized("Oturum geçersiz.");

            return Ok(new
            {
                id = customer.Id,
                name = customer.FullName,
                phone = customer.Phone,
                email = customer.Email,
                company = customer.CompanyName,
                lastLoginDate = customer.LastLoginDate
            });
        }

        [AllowAnonymous]
        [HttpGet("public-auth/orders")]
        public async Task<IActionResult> GetPublicCustomerOrders([FromQuery] string publicToken, [FromQuery] string sessionToken)
        {
            var customer = await ValidatePublicSessionAsync(publicToken, sessionToken);
            if (customer == null) return Unauthorized("Oturum geçersiz.");

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId == customer.Id)
                .OrderByDescending(o => o.CreatedDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.Status,
                    o.TotalAmount,
                    o.CreatedDate,
                    o.PaymentMethod,
                    o.DeliveryCity,
                    itemCount = o.Items.Count
                })
                .ToListAsync();

            return Ok(orders);
        }

        [AllowAnonymous]
        [HttpGet("public-auth/orders/{orderId:guid}")]
        public async Task<IActionResult> GetPublicCustomerOrderDetail(
            Guid orderId,
            [FromQuery] string publicToken,
            [FromQuery] string sessionToken)
        {
            var customer = await ValidatePublicSessionAsync(publicToken, sessionToken);
            if (customer == null) return Unauthorized("Oturum geçersiz.");

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customer.Id);

            if (order == null) return NotFound("Sipariş bulunamadı.");

            return Ok(new
            {
                order.Id,
                order.OrderNumber,
                order.Status,
                order.TotalAmount,
                order.CreatedDate,
                order.CustomerName,
                order.CustomerPhone,
                order.CustomerEmail,
                order.DeliveryAddress,
                order.DeliveryCity,
                order.DeliveryDistrict,
                order.DeliveryNote,
                order.PaymentMethod,
                items = order.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    i.Quantity,
                    i.UnitPrice,
                    lineTotal = i.UnitPrice * i.Quantity,
                    product = i.Product == null
                        ? null
                        : new
                        {
                            i.Product.Id,
                            i.Product.Code,
                            i.Product.Name,
                            i.Product.ImageUrl,
                            i.Product.Description
                        }
                })
            });
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
