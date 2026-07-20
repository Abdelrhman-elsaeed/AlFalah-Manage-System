using System.Text;
using System.Text.Json;
using AlFalah.Api.Middlewares;
using AlFalah.Infrastructure;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Identity.Web;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ─────────────────────────────────────────────────────────────────

builder.Services
    .AddControllers(options =>
    {
        // Accept UTF-8 body even when Content-Type omits the charset (some HTTP clients
        // — notably older curl invocations and certain Angular HttpClient configurations
        // — send `application/json` without `; charset=utf-8`). ASP.NET Core's defaults
        // already handle the well-formed case; this is a defense-in-depth for clients
        // that mis-declare the body. See D-30 (Arabic Unicode column + UTF-8 body fix).
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// Force UTF-8 request body decoding across the pipeline. Without this, a request
// that ships `application/json` WITHOUT a `charset=utf-8` parameter can fall back to
// the server's default codepage (cp1252 on Windows) and corrupt any non-Latin text.
// With `RequestFormLimits`/`FormOptions` plus the UTF8 reader override below, Arabic
// payloads survive the trip from the wire into the action method. See D-30.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("ar-SA");
    options.SupportedCultures = new[] { new System.Globalization.CultureInfo("ar-SA"), new System.Globalization.CultureInfo("en-US") };
    options.SupportedUICultures = new[] { new System.Globalization.CultureInfo("ar-SA"), new System.Globalization.CultureInfo("en-US") };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

// Infrastructure (EF Core, Identity, services)
builder.Services.AddInfrastructure(builder.Configuration);

// FluentValidation (Phase 2) — scan all assemblies referenced from the API host.
builder.Services.AddValidatorsFromAssemblyContaining<AlFalah.Application.DTOs.Auth.AuthResponseDto>();

// ─── JWT Authentication ───────────────────────────────────────────────────────

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Entra is deliberately a second scheme: existing administration JWT login remains unchanged.
builder.Services.AddAuthentication()
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"), jwtBearerScheme: "Entra")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TeacherOneDriveAccess", policy =>
    {
        policy.AddAuthenticationSchemes("Entra");
        policy.RequireAuthenticatedUser();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("teacher-drive", limiter =>
    {
        limiter.PermitLimit = 40;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = builder.Configuration.GetValue<long?>("TeacherDrive:MaxUploadBytes") ?? 250L * 1024 * 1024);

// ─── CORS ─────────────────────────────────────────────────────────────────────

builder.Services.AddCors(options =>
{
    options.AddPolicy("AlFalahCors", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                             ?? new[] { "http://localhost:4200" };
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── Swagger ─────────────────────────────────────────────────────────────────

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Al-Falah Schools Evaluation System API",
        Version = "v1",
        Description = "نظام تقييم مدارس الفلاح"
    });

    // JWT Bearer support in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Build ────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────────────────────

// Defense-in-depth (D-30): force UTF-8 reading for any incoming request body that
// declares application/json (or */*) but omits the charset parameter. Without this,
// the default reader can fall back to the host's ANSI codepage (cp1252 on Windows)
// and silently replace every non-Latin byte with '?' BEFORE the body reaches the
// JSON deserializer — the data is already lost by then.
//
// We use `EnableBuffering()` so the middleware can peek at the body, then if the
// content type lacks a charset and the payload looks like JSON, we re-decode the
// buffered bytes as UTF-8 and replace the request body with the corrected stream.
// The seed row in D-30 (school #9 from the previous Phase 4 session, body sent via
// `curl --data`) is the live evidence: bytes stored were 0x3F00 ('?').
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method))
    {
        var ct = context.Request.ContentType;
        if (!string.IsNullOrEmpty(ct)
            && ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            && !ct.Contains("charset=", StringComparison.OrdinalIgnoreCase))
        {
            // Force UTF-8 so System.Text.Json does the right thing.
            context.Request.ContentType = "application/json; charset=utf-8";
            if (context.Request.Body != null)
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                _ = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }
        }
    }
    await next();
});

app.UseRequestLocalization();

// Global exception handling must be first (still applied after our D-30 reader).
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Al-Falah API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AlFalahCors");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// The SPA needs only these public Entra identifiers to acquire a delegated
// access token. Keeping the values here removes the fragile requirement to
// hand-edit index.html at every deployment. ClientSecret is deliberately
// never exposed by this endpoint.
app.MapGet("/api/v1/auth/entra-config", (IConfiguration configuration) =>
{
    var clientId = configuration["AzureAd:ClientId"];
    var tenantId = configuration["AzureAd:TenantId"];
    var apiScope = configuration["AzureAd:ApiScope"];
    return Results.Ok(new
    {
        clientId,
        tenantId,
        apiScope,
        isConfigured = !string.IsNullOrWhiteSpace(clientId)
                       && !string.IsNullOrWhiteSpace(tenantId)
                       && !string.IsNullOrWhiteSpace(apiScope)
    });
}).AllowAnonymous();

// ─── Database Migration and Seeding ──────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AlFalahDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();

        logger.LogInformation("Running database seeder...");
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database migration or seeding.");
        throw;
    }
}

app.Run();
