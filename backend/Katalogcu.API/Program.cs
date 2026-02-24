using Katalogcu.Infrastructure.Persistence;
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

var builder = WebApplication.CreateBuilder(args);

// ========================================================
// 1. SERVİSLERİN KAYDEDİLMESİ (DEPENDENCY INJECTION)
// ========================================================

// BÜYÜK DOSYA YÜKLEME LİMİTLERİ (PDF/Resim için)
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});

// Genel HttpClient Fabrikası
builder.Services.AddHttpClient(); 

// Yardımcı Servisler
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<CatalogProcessorService>();
builder.Services.AddScoped<IPublicLinkService, PublicLinkService>();

// 🔥 KUYRUK SİSTEMİ (BACKGROUND JOB) 🔥
// 1. Kuyruğu Singleton yapıyoruz (Tüm uygulama aynı sırayı kullansın)
builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx => 
{
    return new BackgroundTaskQueue(100); // Kapasite: 100 Dosya
});

// 2. Arka Plan İşçisini (Worker) başlatıyoruz
builder.Services.AddHostedService<QueuedHostedService>();


// 🔥 AI SERVİS ENTEGRASYONU (POLLY İLE GÜÇLENDİRİLDİ) 🔥
builder.Services.AddHttpClient<IPartalogAiService, PartalogAiService>(client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:8000/"); 
    client.Timeout = TimeSpan.FromMinutes(10); // Timeout süresini biraz artırdık
})
.AddPolicyHandler(GetRetryPolicy()); // 👈 Hata Telafisi Eklendi

// Named HttpClient for direct proxying (e.g. SSE streaming)
builder.Services.AddHttpClient("PartalogAi", client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:8000/");
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Controller ve JSON Ayarları
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// 🔥 VERİTABANI BAĞLANTISI (PostgreSQL + Vektör Desteği) 🔥
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), x => 
    {
        x.UseVector(); 
    }));

// JWT Authentication Ayarları
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? "bu_cok_gizli_ve_uzun_bir_test_anahtaridir_123456");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
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
            policy.WithOrigins("http://localhost:4200", "http://localhost:4200/") 
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .SetIsOriginAllowed(_ => true)
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
});

var app = builder.Build();

// Uygulama açılırken bekleyen migration'ları uygula
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    EnsureStockMovementTable(db);
}

// ========================================================
// 2. MIDDLEWARE
// ========================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); 
app.UseCors("AllowAngularApp");
app.UseRateLimiter();
app.UseAuthentication(); 
app.UseAuthorization();  

app.MapControllers();

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

static void EnsureStockMovementTable(AppDbContext db)
{
    // Bu tabloyu migration beklemeden güvenli şekilde oluşturuyoruz.
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "StockMovements" (
            "Id" uuid NOT NULL,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "UserId" uuid NOT NULL,
            "ProductId" uuid NOT NULL,
            "ProductCode" character varying(128) NOT NULL,
            "ProductName" character varying(512) NOT NULL,
            "PreviousQuantity" integer NOT NULL,
            "DeltaQuantity" integer NOT NULL,
            "NewQuantity" integer NOT NULL,
            "MovementType" character varying(32) NOT NULL,
            "Reason" character varying(1024) NOT NULL,
            "Source" character varying(128) NULL,
            "ActorName" character varying(256) NULL,
            "ReferenceId" character varying(128) NULL,
            CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_StockMovements_Products_ProductId"
                FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
        );
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE INDEX IF NOT EXISTS "IX_StockMovements_UserId_CreatedDate"
        ON "StockMovements" ("UserId", "CreatedDate" DESC);
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE INDEX IF NOT EXISTS "IX_StockMovements_ProductId_CreatedDate"
        ON "StockMovements" ("ProductId", "CreatedDate" DESC);
        """);

    db.Database.ExecuteSqlRaw("""
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM information_schema.table_constraints
                WHERE constraint_name = 'FK_StockMovements_Products_ProductId'
                  AND table_name = 'StockMovements'
            ) THEN
                ALTER TABLE "StockMovements" DROP CONSTRAINT "FK_StockMovements_Products_ProductId";
            END IF;

            ALTER TABLE "StockMovements"
                ADD CONSTRAINT "FK_StockMovements_Products_ProductId"
                FOREIGN KEY ("ProductId") REFERENCES "Products"("Id") ON DELETE CASCADE;
        END
        $$;
        """);
}
