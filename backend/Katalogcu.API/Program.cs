using Katalogcu.Infrastructure.Persistence;
using Katalogcu.Application;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Katalogcu.API.Services;
using Microsoft.AspNetCore.Http.Features; 
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Polly; // 🔥 Polly için
using Polly.Extensions.Http; // 🔥 Polly HTTP Extensions için
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var defaultMaxBodySizeMb = builder.Configuration.GetValue<int?>("RequestLimits:DefaultMaxBodySizeMb") ?? 50;
if (defaultMaxBodySizeMb is < 1 or > 512)
{
    throw new InvalidOperationException("RequestLimits:DefaultMaxBodySizeMb 1 ile 512 arasında olmalıdır.");
}

var defaultMaxBodySizeBytes = defaultMaxBodySizeMb * 1024L * 1024L;

// ========================================================
// 1. SERVİSLERİN KAYDEDİLMESİ (DEPENDENCY INJECTION)
// ========================================================

// BÜYÜK DOSYA YÜKLEME LİMİTLERİ (PDF/Resim için)
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = 2 * 1024 * 1024;
    options.MultipartBodyLengthLimit = defaultMaxBodySizeBytes;
    options.MemoryBufferThreshold = 1024 * 1024;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = defaultMaxBodySizeBytes;
});

// Genel HttpClient Fabrikası
builder.Services.AddHttpClient(); 
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ProductFeatureOptions>(builder.Configuration.GetSection("ProductFeatures"));
builder.Services.AddSingleton<IProductFeaturePolicy, ProductFeaturePolicy>();

// Yardımcı Servisler
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<CatalogProcessorService>();
builder.Services.AddScoped<IPublicLinkService, PublicLinkService>();
builder.Services.AddScoped<IPublicCatalogLinkService, PublicCatalogLinkService>();
builder.Services.AddScoped<IPublicAccessTokenService, PublicAccessTokenService>();
builder.Services.AddScoped<IChatStreamProxyService, ChatStreamProxyService>();
builder.Services.AddScoped<IVisualFeedbackService, VisualFeedbackService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IHotspotDetectionService, HotspotDetectionService>();
builder.Services.AddScoped<ICatalogPdfPageService, CatalogPdfPageService>();
builder.Services.AddScoped<ICatalogPageFileService, CatalogPageFileService>();
builder.Services.AddScoped<ICatalogCoverMetadataService, CatalogCoverMetadataService>();
builder.Services.AddScoped<IChatFeedbackStore, ChatFeedbackJsonlStore>();
builder.Services.AddScoped<ICatalogAiBackgroundProcessor, CatalogAiBackgroundProcessor>();
builder.Services.AddScoped<IAiUsageQuotaService, AiUsageQuotaService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructureServices();

// 🔥 KUYRUK SİSTEMİ (BACKGROUND JOB) 🔥
// 1. Kuyruğu Singleton yapıyoruz (Tüm uygulama aynı sırayı kullansın)
builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx => 
{
    return new BackgroundTaskQueue(100); // Kapasite: 100 Dosya
});

// 2. Arka Plan İşçisini (Worker) başlatıyoruz
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddHostedService<CatalogAiOutboxWorker>();


// 🔥 AI SERVİS ENTEGRASYONU (POLLY İLE GÜÇLENDİRİLDİ) 🔥
var aiServiceBaseUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://127.0.0.1:8000";
if (!aiServiceBaseUrl.EndsWith("/"))
{
    aiServiceBaseUrl += "/";
}

builder.Services.AddHttpClient<IPartalogAiService, PartalogAiService>(client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(10); // Timeout süresini biraz artırdık
})
.AddPolicyHandler(GetRetryPolicy()); // 👈 Hata Telafisi Eklendi

// Named HttpClient for direct proxying (e.g. SSE streaming)
builder.Services.AddHttpClient("PartalogAi", client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Controller ve JSON Ayarları
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// 🔥 VERİTABANI BAĞLANTISI (PostgreSQL + Vektör Desteği) 🔥
builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), x =>
        {
            x.UseVector();
        }));
builder.Services.AddHealthChecks();

// JWT Authentication Ayarları
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Trim().Length < 32)
{
    throw new InvalidOperationException("JwtSettings:SecretKey zorunludur ve en az 32 karakter olmalıdır.");
}

var publicLinkSecret = builder.Configuration["PublicLink:SecretKey"];
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (builder.Environment.IsProduction())
{
    if (jwtSecret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("JwtSettings:SecretKey production ortamında CHANGE_ME olamaz.");
    }

    if (string.IsNullOrWhiteSpace(publicLinkSecret) || publicLinkSecret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("PublicLink:SecretKey production ortamında geçerli bir secret olmalıdır.");
    }

    if (string.IsNullOrWhiteSpace(defaultConnection) || defaultConnection.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection production ortamında geçerli olmalıdır.");
    }
}

var secretKey = Encoding.ASCII.GetBytes(jwtSecret);

// CORS origins (prod ortamında config zorunlu, development'ta localhost fallback var)
var configuredCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Select(x => x.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? [];

if (configuredCorsOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    configuredCorsOrigins = ["http://localhost:4200", "http://127.0.0.1:4200"];
}

if (configuredCorsOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:AllowedOrigins ayarı zorunludur.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateIssuer = true, 
        ValidIssuer = jwtSettings["Issuer"] ?? "KatalogcuAPI",
        ValidateAudience = true, 
        ValidAudience = jwtSettings["Audience"] ?? "KatalogcuClient",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole("admin"));

    options.AddPolicy("PlatformAdminOnly", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context =>
            {
                var roles = context.User.FindAll(ClaimTypes.Role).Select(x => x.Value);
                return roles.Any(role => role.Equals("platformadmin", StringComparison.OrdinalIgnoreCase));
            }));

    options.AddPolicy("PrivilegedUser", policy =>
        policy.RequireAuthenticatedUser()
            .RequireAssertion(context =>
            {
                var roles = context.User.FindAll(ClaimTypes.Role).Select(x => x.Value);
                return roles.Any(role =>
                    role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                    role.Equals("owner", StringComparison.OrdinalIgnoreCase));
            }));
});

// Swagger Konfigürasyonu
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Katalogcu API", Version = "v1" });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Örnek: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "oauth2", Name = "Bearer", In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// CORS AYARLARI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins(configuredCorsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Public endpoint abuse protection (IP bazlı)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = static (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        return new ValueTask(
            context.HttpContext.Response.WriteAsync(
                "{\"success\":false,\"message\":\"Çok fazla istek gönderildi. Lütfen kısa süre sonra tekrar deneyin.\"}",
                token));
    };

    options.AddPolicy("public-chat", httpContext =>
    {
        var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true;
        if (isAuthenticated)
        {
            return RateLimitPartition.GetNoLimiter("auth-user");
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"public-chat:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("public-feedback", httpContext =>
    {
        var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true;
        if (isAuthenticated)
        {
            return RateLimitPartition.GetNoLimiter("auth-user-feedback");
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"public-feedback:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("public-order", httpContext =>
    {
        var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true;
        if (isAuthenticated)
        {
            return RateLimitPartition.GetNoLimiter("auth-user-order");
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"public-order:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("auth-login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth-login:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

var featurePolicy = app.Services.GetRequiredService<IProductFeaturePolicy>();
app.Logger.LogInformation(
    "ProductFeatures mode => AI: {AiEnabled}, ECommerce: {EcommerceEnabled}, UpgradePrompts: {UpgradePromptsEnabled}",
    featurePolicy.AiEnabled,
    featurePolicy.EcommerceEnabled,
    featurePolicy.UpgradePromptsEnabled);

if (app.Environment.IsProduction() && (featurePolicy.AiEnabled || featurePolicy.EcommerceEnabled))
{
    app.Logger.LogWarning(
        "Production mode is running with premium modules enabled (AI={AiEnabled}, ECommerce={EcommerceEnabled}).",
        featurePolicy.AiEnabled,
        featurePolicy.EcommerceEnabled);
}

// Uygulama açılırken bekleyen migration'ları uygula
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pendingMigrations = db.Database.GetPendingMigrations().ToArray();
    if (pendingMigrations.Length > 0)
    {
        app.Logger.LogInformation(
            "Applying {Count} pending EF migration(s): {Migrations}",
            pendingMigrations.Length,
            string.Join(", ", pendingMigrations));
    }

    db.Database.Migrate();

    var stillPendingMigrations = db.Database.GetPendingMigrations().ToArray();
    if (stillPendingMigrations.Length > 0)
    {
        throw new InvalidOperationException(
            $"Database startup check failed. Pending migrations remain: {string.Join(", ", stillPendingMigrations)}");
    }

    if (db.Database.HasPendingModelChanges())
    {
        throw new InvalidOperationException(
            "Database startup check failed. EF model has pending changes. Add and apply a migration before starting API.");
    }

    app.Logger.LogInformation("Database startup check passed: migrations and EF model are in sync.");
}

// ========================================================
// 2. MIDDLEWARE
// ========================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("GlobalExceptionHandler");

            if (feature?.Error != null)
            {
                logger.LogError(feature.Error, "Unhandled exception on path {Path}", context.Request.Path);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Beklenmeyen bir sistem hatası oluştu."
            });
        });
    });

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});
app.UseCors("AllowAngularApp");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<UserSuspensionMiddleware>();
app.UseMiddleware<ModuleFeatureGateMiddleware>();
app.UseMiddleware<CatalogPlanLimitMiddleware>();
app.UseAuthorization();  

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    if (!canConnect)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Service Unavailable",
            detail: "Database connection check failed.");
    }

    return Results.Ok(new { status = "ready" });
});
app.MapGet("/health/migrations", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
    var hasPendingModelChanges = db.Database.HasPendingModelChanges();

    if (pendingMigrations.Length > 0 || hasPendingModelChanges)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Migration check failed",
            detail: $"PendingMigrations={pendingMigrations.Length}, PendingModelChanges={hasPendingModelChanges}");
    }

    var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
    return Results.Ok(new
    {
        status = "ok",
        appliedCount = appliedMigrations.Length,
        latestApplied = appliedMigrations.LastOrDefault()
    });
});

app.Run();


// ========================================================
// 🛠️ YARDIMCI METOTLAR (POLLY POLİTİKASI)
// ========================================================
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        // 1. Geçici Hataları Yakala (5xx, 408 Request Timeout)
        .HandleTransientHttpError()
        // 2. VEYA Google "Çok İstek Attın" (429 Too Many Requests) derse yakala
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        // 3. Bekle ve Tekrar Dene (Exponential Backoff)
        // İlk deneme: 2sn, İkinci: 4sn, Üçüncü: 8sn bekle.
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
