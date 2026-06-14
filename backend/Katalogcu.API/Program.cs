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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

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
builder.Services.Configure<CatalogAiProcessingOptions>(builder.Configuration.GetSection(CatalogAiProcessingOptions.SectionName));
builder.Services.Configure<ErpGatewayOptions>(builder.Configuration.GetSection(ErpGatewayOptions.SectionName));
builder.Services.Configure<ProductFeatureOptions>(builder.Configuration.GetSection("ProductFeatures"));
builder.Services.Configure<AiServiceOptions>(builder.Configuration.GetSection(AiServiceOptions.SectionName));
builder.Services.Configure<DistributedRateLimitOptions>(builder.Configuration.GetSection(DistributedRateLimitOptions.SectionName));
builder.Services.Configure<DataProtectionKeyRingOptions>(builder.Configuration.GetSection(DataProtectionKeyRingOptions.SectionName));
builder.Services.AddSingleton<IProductFeaturePolicy, ProductFeaturePolicy>();

var catalogAiProcessingOptions = builder.Configuration.GetSection(CatalogAiProcessingOptions.SectionName).Get<CatalogAiProcessingOptions>()
    ?? new CatalogAiProcessingOptions();

var defaultConnection = FirstNonEmpty(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    builder.Configuration["ConnectionStrings:DefaultConnection"]);
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
builder.Services.AddScoped<IEmbedOriginService, EmbedOriginService>();
builder.Services.AddScoped<IEmbedAnalyticsService, EmbedAnalyticsService>();
builder.Services.AddScoped<IEmbedDomainVerificationService, EmbedDomainVerificationService>();
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
builder.Services.AddScoped<IErpGatewayService, ErpGatewayService>();
builder.Services.AddScoped<IErpGatewayStrategy, SnapshotErpGatewayStrategy>();
builder.Services.AddScoped<IAiUsageQuotaService, AiUsageQuotaService>();
builder.Services.AddScoped<IProductionReadinessService, ProductionReadinessService>();
builder.Services.Configure<AiCapacityOptions>(builder.Configuration.GetSection("AiCapacity"));
builder.Services.AddSingleton<IAiCapacityGuard, AiCapacityGuard>();
builder.Services.AddSingleton<IDistributedPublicChatRateLimiter, RedisDistributedPublicChatRateLimiter>();
builder.Services.AddSingleton<CatalogAiHangfireFilter>();
builder.Services.AddApplication();
builder.Services.AddInfrastructureServices();

// 🔥 AI SERVİS ENTEGRASYONU (POLLY İLE GÜÇLENDİRİLDİ) 🔥
var aiServiceOptions = builder.Configuration.GetSection(AiServiceOptions.SectionName).Get<AiServiceOptions>()
    ?? new AiServiceOptions();
var aiServiceBaseUrl = aiServiceOptions.BaseUrl;
if (!aiServiceBaseUrl.EndsWith("/"))
{
    aiServiceBaseUrl += "/";
}

builder.Services.AddHttpClient<IPartalogAiService, PartalogAiService>(client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = aiServiceOptions.GetLongRunningTimeout();
})
.AddPolicyHandler(GetRetryPolicy()); // 👈 Hata Telafisi Eklendi

// Named HttpClient for direct proxying (e.g. SSE streaming)
builder.Services.AddHttpClient("PartalogAi", client =>
{
    client.BaseAddress = new Uri(aiServiceBaseUrl);
    client.Timeout = aiServiceOptions.GetStreamTimeout();
});

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

var enableCatalogAiHangfireServer = builder.Configuration.GetValue<bool?>("BackgroundProcessing:EnableCatalogAiServer") ?? true;
var enableDefaultHangfireServer = builder.Configuration.GetValue<bool?>("BackgroundProcessing:EnableDefaultServer") ?? true;

if (enableCatalogAiHangfireServer || enableDefaultHangfireServer)
{
    builder.Services.AddHangfireServer(options =>
    {
        var queues = new List<string>();
        if (enableCatalogAiHangfireServer)
        {
            queues.Add(CatalogAiHangfireJob.QueueName);
        }

        if (enableDefaultHangfireServer)
        {
            queues.Add("default");
        }

        options.Queues = queues.ToArray();
        options.WorkerCount = catalogAiProcessingOptions.GetNormalizedWorkerCount();
    });
}

builder.Services.AddHealthChecks();

var dataProtectionOptions = GetDataProtectionKeyRingOptions(builder.Configuration);
var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionOptions.ApplicationName);

ConfigureDataProtectionKeyRing(
    dataProtectionBuilder,
    dataProtectionOptions,
    builder.Environment);

// JWT Authentication Ayarları
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = JwtSecretResolver.Resolve(builder.Configuration);
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Trim().Length < 32)
{
    throw new InvalidOperationException("JwtSettings:SecretKey zorunludur ve en az 32 karakter olmalıdır.");
}

var publicLinkSecret = FirstNonEmpty(
    builder.Configuration["PublicLink:SecretKey"],
    builder.Configuration["PublicLinkSecret"]);
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

var secretKey = Encoding.UTF8.GetBytes(jwtSecret);

// CORS origins (prod ortamında config zorunlu, development'ta localhost fallback var)
var configuredCorsOrigins = GetConfiguredCorsOrigins(builder.Configuration);

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

    options.AddPolicy("public-embed-events", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"public-embed-events:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
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
GlobalJobFilters.Filters.Add(app.Services.GetRequiredService<CatalogAiHangfireFilter>());

var featurePolicy = app.Services.GetRequiredService<IProductFeaturePolicy>();
app.Logger.LogInformation(
    "ProductFeatures mode => Chatbot: {ChatbotEnabled}, CatalogAnalysis: {CatalogAnalysisEnabled}, ECommerce: {EcommerceEnabled}, UpgradePrompts: {UpgradePromptsEnabled}, PlanManagement: {PlanManagementEnabled}",
    featurePolicy.ChatbotEnabled,
    featurePolicy.CatalogAnalysisEnabled,
    featurePolicy.EcommerceEnabled,
    featurePolicy.UpgradePromptsEnabled,
    featurePolicy.PlanManagementEnabled);

if (app.Environment.IsProduction() && (featurePolicy.ChatbotEnabled || featurePolicy.CatalogAnalysisEnabled || featurePolicy.EcommerceEnabled))
{
    app.Logger.LogWarning(
        "Production mode is running with premium modules enabled (Chatbot={ChatbotEnabled}, CatalogAnalysis={CatalogAnalysisEnabled}, ECommerce={EcommerceEnabled}).",
        featurePolicy.ChatbotEnabled,
        featurePolicy.CatalogAnalysisEnabled,
        featurePolicy.EcommerceEnabled);
}

var runMigrationsOnStartup = builder.Configuration.GetValue("Database:RunMigrationsOnStartup", true);
if (runMigrationsOnStartup)
{
    // Uygulama açılırken bekleyen migration'ları uygula
    using var scope = app.Services.CreateScope();
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
}
else
{
    app.Logger.LogWarning(
        "Database migration startup check is disabled by Database:RunMigrationsOnStartup=false. Use this only for local smoke tests or externally managed migrations.");
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
app.UseMiddleware<DynamicEmbedCorsMiddleware>();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<DistributedPublicChatRateLimitMiddleware>();
app.UseMiddleware<UserSuspensionMiddleware>();
app.UseMiddleware<ModuleFeatureGateMiddleware>();
app.UseMiddleware<CatalogPlanLimitMiddleware>();
app.UseAuthorization();  

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapGet("/health/ready", async (AppDbContext db, IAiCapacityGuard aiCapacityGuard, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    if (!canConnect)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Service Unavailable",
            detail: "Database connection check failed.");
    }

    var capacityHealth = await aiCapacityGuard.CheckHealthAsync(cancellationToken);
    if (!capacityHealth.Ready)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Service Unavailable",
            detail: $"AI capacity dependency check failed: {capacityHealth.Error}");
    }

    return Results.Ok(new
    {
        status = "ready",
        capacity = new
        {
            ready = capacityHealth.Ready,
            mode = capacityHealth.Mode,
            provider = capacityHealth.Provider,
            latencyMs = capacityHealth.LatencyMs
        }
    });
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

static string? FirstNonEmpty(params string?[] values)
{
    foreach (var value in values)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }
    }

    return null;
}

static string[] GetConfiguredCorsOrigins(IConfiguration configuration)
{
    var sectionOrigins = configuration.GetSection("Cors:AllowedOrigins")
        .Get<string[]>()?
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (sectionOrigins is { Length: > 0 })
    {
        return sectionOrigins;
    }

    var aliasOrigins = FirstNonEmpty(
        configuration["CorsOrigins"],
        configuration["AllowedOrigins"]);

    if (string.IsNullOrWhiteSpace(aliasOrigins))
    {
        return [];
    }

    return aliasOrigins
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static DataProtectionKeyRingOptions GetDataProtectionKeyRingOptions(IConfiguration configuration)
{
    var options = configuration
        .GetSection(DataProtectionKeyRingOptions.SectionName)
        .Get<DataProtectionKeyRingOptions>()
        ?? new DataProtectionKeyRingOptions();

    options.Provider = FirstNonEmpty(
        options.Provider,
        configuration["DATA_PROTECTION_PROVIDER"])
        ?? "";

    options.ApplicationName = FirstNonEmpty(
        options.ApplicationName,
        configuration["DATA_PROTECTION_APPLICATION_NAME"])
        ?? "Katalogcu.API";

    options.KeysDirectory = FirstNonEmpty(
        options.KeysDirectory,
        configuration["DATA_PROTECTION_KEYS_DIRECTORY"])
        ?? "";

    options.RedisConnectionString = FirstNonEmpty(
        options.RedisConnectionString,
        configuration["DATA_PROTECTION_REDIS_CONNECTION_STRING"])
        ?? "";

    options.RedisKey = FirstNonEmpty(
        options.RedisKey,
        configuration["DATA_PROTECTION_REDIS_KEY"])
        ?? "partalog:data-protection:keys";

    options.KeyEncryptionKey = FirstNonEmpty(
        options.KeyEncryptionKey,
        configuration["DATA_PROTECTION_KEY_ENCRYPTION_KEY"])
        ?? "";

    return options;
}

static void ConfigureDataProtectionKeyRing(
    IDataProtectionBuilder builder,
    DataProtectionKeyRingOptions options,
    IWebHostEnvironment environment)
{
    var provider = FirstNonEmpty(options.Provider);
    if (string.IsNullOrWhiteSpace(provider))
    {
        provider = !string.IsNullOrWhiteSpace(options.RedisConnectionString)
            ? "Redis"
            : !string.IsNullOrWhiteSpace(options.KeysDirectory)
                ? "FileSystem"
                : "";
    }

    ConfigureDataProtectionXmlEncryption(builder, options, environment);

    if (provider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            throw new InvalidOperationException(
                "DataProtection:Provider=Redis seçildi ama DataProtection:RedisConnectionString boş.");
        }

        builder.Services.Configure<KeyManagementOptions>(keyManagementOptions =>
        {
            keyManagementOptions.XmlRepository = new RedisDataProtectionXmlRepository(
                options.RedisConnectionString,
                options.RedisKey);
        });
        return;
    }

    if (provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(options.KeysDirectory))
        {
            throw new InvalidOperationException(
                "DataProtection:Provider=FileSystem seçildi ama DataProtection:KeysDirectory boş.");
        }

        Directory.CreateDirectory(options.KeysDirectory);
        builder.PersistKeysToFileSystem(new DirectoryInfo(options.KeysDirectory));
        return;
    }

    if (environment.IsProduction())
    {
        throw new InvalidOperationException(
            "Production ortamında DataProtection:Provider zorunludur. Redis veya FileSystem key-ring yapılandırın.");
    }
}

static void ConfigureDataProtectionXmlEncryption(
    IDataProtectionBuilder builder,
    DataProtectionKeyRingOptions options,
    IWebHostEnvironment environment)
{
    if (AesGcmDataProtectionXmlEncryptor.IsConfigured(options.KeyEncryptionKey))
    {
        builder.Services.Configure<KeyManagementOptions>(keyManagementOptions =>
        {
            keyManagementOptions.XmlEncryptor = new AesGcmDataProtectionXmlEncryptor(options.KeyEncryptionKey);
        });
        return;
    }

    if (environment.IsProduction())
    {
        throw new InvalidOperationException(
            "Production ortamında DataProtection:KeyEncryptionKey zorunludur. 32 byte random base64 secret yapılandırın.");
    }
}
