using Katalogcu.Infrastructure.Persistence;
using Katalogcu.Application;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
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
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = JwtSecretResolver.Resolve(builder.Configuration);
var publicLinkSecret = builder.Configuration["PublicLink:SecretKey"]?.Trim() ?? string.Empty;

if (builder.Environment.IsDevelopment())
{
    if (!SigningSecretPolicy.IsAcceptable(jwtSecret))
    {
        jwtSecret = SigningSecretPolicy.Generate();
        builder.Configuration["JwtSettings:SecretKey"] = jwtSecret;
    }

    if (!SigningSecretPolicy.IsAcceptable(publicLinkSecret))
    {
        publicLinkSecret = SigningSecretPolicy.Generate();
        builder.Configuration["PublicLink:SecretKey"] = publicLinkSecret;
    }
}
else
{
    if (!SigningSecretPolicy.IsAcceptable(jwtSecret))
    {
        throw new InvalidOperationException("JwtSettings:SecretKey güvenli bir secret manager veya ortam değişkeni üzerinden sağlanmalıdır.");
    }

    if (!SigningSecretPolicy.IsAcceptable(publicLinkSecret))
    {
        throw new InvalidOperationException("PublicLink:SecretKey güvenli bir secret manager veya ortam değişkeni üzerinden sağlanmalıdır.");
    }
}

if (CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(jwtSecret),
        Encoding.UTF8.GetBytes(publicLinkSecret)))
{
    throw new InvalidOperationException("JWT ve public-link tokenları farklı imzalama anahtarları kullanmalıdır.");
}

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
builder.Services.Configure<CatalogAiProcessingOptions>(builder.Configuration.GetSection(CatalogAiProcessingOptions.SectionName));
builder.Services.Configure<ErpGatewayOptions>(builder.Configuration.GetSection(ErpGatewayOptions.SectionName));
builder.Services.Configure<ProductFeatureOptions>(builder.Configuration.GetSection("ProductFeatures"));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));
builder.Services.Configure<AiServiceOptions>(builder.Configuration.GetSection(AiServiceOptions.SectionName));
builder.Services.Configure<DistributedRateLimitOptions>(builder.Configuration.GetSection(DistributedRateLimitOptions.SectionName));
builder.Services.Configure<DataProtectionKeyRingOptions>(builder.Configuration.GetSection(DataProtectionKeyRingOptions.SectionName));
builder.Services.Configure<AiCapacityOptions>(builder.Configuration.GetSection("AiCapacity"));
builder.Services.AddSingleton<IProductFeaturePolicy, ProductFeaturePolicy>();

var catalogAiProcessingOptions = builder.Configuration.GetSection(CatalogAiProcessingOptions.SectionName).Get<CatalogAiProcessingOptions>()
    ?? new CatalogAiProcessingOptions();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection zorunludur.");
}

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
builder.Services.AddScoped<CatalogAiHangfireJob>();
builder.Services.AddScoped<IExternalSiteCrawlBackgroundProcessor, ExternalSiteCrawlBackgroundProcessor>();
builder.Services.AddScoped<ExternalSiteCrawlHangfireJob>();
builder.Services.AddScoped<IErpGatewayService, ErpGatewayService>();
builder.Services.AddScoped<IErpGatewayStrategy, SnapshotErpGatewayStrategy>();
builder.Services.AddScoped<IAiUsageQuotaService, AiUsageQuotaService>();
builder.Services.AddScoped<IAiCapacityGuard, AiCapacityGuard>();
builder.Services.AddScoped<ICatalogPageAccessTokenService, CatalogPageAccessTokenService>();
builder.Services.AddScoped<IPolicyThresholdActorContext, PolicyThresholdActorContext>();
builder.Services.AddScoped<IPolicyThresholdEvaluationTokenService, PolicyThresholdEvaluationTokenService>();
builder.Services.AddScoped<IProductionReadinessService, ProductionReadinessService>();
builder.Services.AddSingleton<IDistributedPublicChatRateLimiter, RedisDistributedPublicChatRateLimiter>();
builder.Services.AddSingleton<FileStoragePathResolver>();
builder.Services.AddScoped<IFileStorageService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
    var provider = (options.Provider ?? "Local").Trim();
    return provider.Equals("GoogleCloudStorage", StringComparison.OrdinalIgnoreCase)
           || provider.Equals("GCS", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<GoogleCloudFileStorageService>()
        : sp.GetRequiredService<LocalFileStorageService>();
});
builder.Services.AddScoped<LocalFileStorageService>();
builder.Services.AddScoped<GoogleCloudFileStorageService>();
builder.Services.AddSingleton<CatalogAiHangfireFilter>();
builder.Services.AddKatalogcuDataProtection(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructureServices();

// 🔥 AI SERVİS ENTEGRASYONU (POLLY İLE GÜÇLENDİRİLDİ) 🔥
var aiServiceBaseUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://127.0.0.1:8000";
var aiServiceOptions = builder.Configuration.GetSection(AiServiceOptions.SectionName).Get<AiServiceOptions>()
    ?? new AiServiceOptions();
if (!aiServiceBaseUrl.EndsWith("/"))
{
    aiServiceBaseUrl += "/";
}

builder.Services.AddSingleton<ICloudRunIdentityTokenProvider, GoogleCloudRunIdentityTokenProvider>();
builder.Services.AddTransient<CloudRunIdentityTokenHandler>();

builder.Services.AddHttpClient<IPartalogAiService, PartalogAiService>(client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = aiServiceOptions.GetLongRunningTimeout();
})
.AddHttpMessageHandler<CloudRunIdentityTokenHandler>()
.AddPolicyHandler(GetRetryPolicy()); // 👈 Hata Telafisi Eklendi

// Named HttpClient for direct proxying (e.g. SSE streaming)
builder.Services.AddHttpClient("PartalogAi", client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = aiServiceOptions.GetStreamTimeout();
})
.AddHttpMessageHandler<CloudRunIdentityTokenHandler>();

// Controller ve JSON Ayarları
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// 🔥 VERİTABANI BAĞLANTISI (PostgreSQL + Vektör Desteği) 🔥
builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(defaultConnection, x =>
        {
            x.UseVector();
        }));

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(defaultConnection)));

builder.Services.AddHangfireServer(options =>
{
    options.Queues =
    [
        CatalogAiHangfireJob.QueueName,
        "default"
    ];
    options.WorkerCount = catalogAiProcessingOptions.GetNormalizedWorkerCount();
});

builder.Services.AddHealthChecks();

// JWT Authentication Ayarları
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(defaultConnection) ||
        defaultConnection.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
        defaultConnection.Contains("YourPasswordHere", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection production ortamında geçerli olmalıdır.");
    }
}

var secretKey = Encoding.UTF8.GetBytes(jwtSecret);

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
        ValidAudience = jwtSettings["Audience"] ?? "KatalogcuUsers",
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
    var publicChatPermitLimit = builder.Configuration.GetValue<int?>("RateLimits:PublicChatPermitLimit") ?? 20;
    var publicFeedbackPermitLimit = builder.Configuration.GetValue<int?>("RateLimits:PublicFeedbackPermitLimit") ?? 10;
    var publicOrderPermitLimit = builder.Configuration.GetValue<int?>("RateLimits:PublicOrderPermitLimit") ?? 8;
    var authLoginPermitLimit = builder.Configuration.GetValue<int?>("RateLimits:AuthLoginPermitLimit") ?? 6;

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
                PermitLimit = publicChatPermitLimit,
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
                PermitLimit = publicFeedbackPermitLimit,
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
                PermitLimit = publicOrderPermitLimit,
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
                PermitLimit = authLoginPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();
GlobalJobFilters.Filters.Add(app.Services.GetRequiredService<CatalogAiHangfireFilter>());

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

var staticFileProvider = string.IsNullOrWhiteSpace(app.Environment.WebRootPath)
    ? null
    : new ExcludedStaticFileProvider(
        new PhysicalFileProvider(app.Environment.WebRootPath),
        "uploads");

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = staticFileProvider ?? app.Environment.WebRootFileProvider
});
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
