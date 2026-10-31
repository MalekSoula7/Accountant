using System.Text;
using ComptaShield.Api.Hubs;
using ComptaShield.Api.Middleware;
using ComptaShield.Api.Swagger;
using ComptaShield.Audit.Persistence;
using ComptaShield.Audit.Services;
using ComptaShield.FraudShield.Persistence;
using ComptaShield.FraudShield.Services;
using ComptaShield.Ingestion.Persistence;
using ComptaShield.Ingestion.Storage;
using ComptaShield.Ingestion.Validators;
using ComptaShield.Inventory.Persistence;
using ComptaShield.Inventory.Services;
using ComptaShield.LedgerAndBilan.Persistence;
using ComptaShield.LedgerAndBilan.Services;
using ComptaShield.SharedKernel.MultiTenancy;
using ComptaShield.Tenancy.Persistence;
using ComptaShield.Tenancy.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Multi-Tenancy Scoped Context
builder.Services.AddScoped<ScopedTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<ScopedTenantContext>());

// 2. Database Contexts (PostgreSQL with RLS connection interceptor)
var postgresConn = builder.Configuration.GetConnectionString("PostgreSQL") 
    ?? "Host=localhost;Port=5432;Database=comptashield_dev;Username=postgres;Password=postgres";

builder.Services.AddDbContext<TenancyDbContext>(options =>
    options.UseNpgsql(postgresConn));

builder.Services.AddDbContext<IngestionDbContext>((sp, options) =>
    options.UseNpgsql(postgresConn).AddInterceptors(new RlsDbConnectionInterceptor(sp.GetRequiredService<ITenantContext>())));

builder.Services.AddDbContext<FraudDbContext>((sp, options) =>
    options.UseNpgsql(postgresConn).AddInterceptors(new RlsDbConnectionInterceptor(sp.GetRequiredService<ITenantContext>())));

builder.Services.AddDbContext<LedgerDbContext>((sp, options) =>
    options.UseNpgsql(postgresConn).AddInterceptors(new RlsDbConnectionInterceptor(sp.GetRequiredService<ITenantContext>())));

builder.Services.AddDbContext<InventoryDbContext>((sp, options) =>
    options.UseNpgsql(postgresConn).AddInterceptors(new RlsDbConnectionInterceptor(sp.GetRequiredService<ITenantContext>())));

builder.Services.AddDbContext<AuditDbContext>((sp, options) =>
    options.UseNpgsql(postgresConn).AddInterceptors(new RlsDbConnectionInterceptor(sp.GetRequiredService<ITenantContext>())));

// 3. Redis Connection & Permission Checker
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    try
    {
        return ConnectionMultiplexer.Connect(redisConn);
    }
    catch
    {
        // Fallback mock configuration in test/isolated environments
        return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false");
    }
});
builder.Services.AddScoped<IPermissionChecker, RedisPermissionChecker>();

// 4. Object Storage (MinIO S3)
builder.Services.AddSingleton<IMinioClient>(_ =>
    new MinioClient()
        .WithEndpoint(builder.Configuration["Minio:Endpoint"] ?? "localhost:9000")
        .WithCredentials(
            builder.Configuration["Minio:AccessKey"] ?? "minioadmin",
            builder.Configuration["Minio:SecretKey"] ?? "minioadmin")
        .WithSSL(false)
        .Build());
builder.Services.AddScoped<IWormStorageService, S3WormStorageService>();

// 5. Domain Calculation & Forensic Services
builder.Services.AddScoped<DocumentMathAndConfidenceValidator>();
builder.Services.AddScoped<PdfMetadataForensicsScanner>();
builder.Services.AddScoped<SemanticDuplicateDetector>();
builder.Services.AddScoped<ThreeWayMatchingEngine>();
builder.Services.AddScoped<BenfordLawAuditService>();
builder.Services.AddScoped<FraudRectificationService>();
builder.Services.AddScoped<BilanCalculationService>();
builder.Services.AddScoped<CumpValuationCalculator>();
builder.Services.AddScoped<FifoValuationCalculator>();
builder.Services.AddScoped<InventoryReconciliationService>();
builder.Services.AddScoped<MaterialityCalculator>();
builder.Services.AddScoped<MonetaryUnitSamplingSelector>();
builder.Services.AddScoped<SignedDeliverableGenerator>();

// 6. SignalR
builder.Services.AddSignalR();

// 7. JWT Bearer Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ComptaShield_SuperSecret_Jwt_SigningKey_2026_AtLeast32Bytes!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ComptaShield.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ComptaShield.Api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Support token passing in SignalR WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// 8. Swagger / OpenAPI Documentation with Multi-Tenancy Headers
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ComptaShield API",
        Version = "v1",
        Description = "Automated Accounting, Audit & Anti-Fraud Security Operating Platform"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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

    c.OperationFilter<TenantHeaderOperationFilter>();
});

var app = builder.Build();

// 9. Pipeline Configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ComptaShield API v1"));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<TenantScopeMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<FraudAlertHub>("/hubs/fraud-alerts");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.Run();
