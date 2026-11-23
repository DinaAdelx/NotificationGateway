using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NotificationGateway.API.Authentication;
using NotificationGateway.API.Middleware;
using NotificationGateway.API.OpenApi;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Application.Notifications.SubmitNotification;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Infrastructure.BackgroundJobs;
using NotificationGateway.Infrastructure.Persistence;
using NotificationGateway.Infrastructure.Persistence.Repositories;
using NotificationGateway.Infrastructure.Providers.Email;
using NotificationGateway.Infrastructure.Providers.InApp;
using NotificationGateway.Infrastructure.Providers.SignalR;
using NotificationGateway.Infrastructure.Security;
using NotificationGateway.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers & SignalR
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "GenericNotificationGatewaySuperSecretKey_2026_ReplaceInProduction!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NotificationGateway";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NotificationGatewayClients";

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Swagger / OpenAPI with Tenant Context headers & Bearer Auth support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Multi-Tenant Notification Gateway API",
        Version = "v1",
        Description = "Generic, multi-tenant notification gateway for user authentication, template authoring per user/type, and reliable delivery."
    });

    c.OperationFilter<TenantHeaderOperationFilter>();

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using Bearer scheme (e.g. 'Bearer {token}')",
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

// Database Context (SQL Server)
var connString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=NotificationGatewayDb;User Id=YOUR_SQL_USERNAME;Password=YOUR_SQL_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<NotificationGatewayDbContext>(opts =>
{
    opts.UseSqlServer(connString);
});

// Tenant Context & Repositories
builder.Services.AddScoped<NotificationContext>();
builder.Services.AddScoped<INotificationContext>(sp => sp.GetRequiredService<NotificationContext>());
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationTypeRepository, NotificationTypeRepository>();
builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IDeliveryRepository, DeliveryRepository>();
builder.Services.AddScoped<IProviderConfigurationRepository, ProviderConfigurationRepository>();
builder.Services.AddScoped<IRoutingRepository, RoutingRepository>();

// Core Services & Auth
builder.Services.AddScoped<IPayloadValidator, JsonSchemaPayloadValidator>();
builder.Services.AddScoped<ITemplateRenderer, HandlebarsTemplateRenderer>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<ICredentialEncryptor>(new AesCredentialEncryptor(
    builder.Configuration["Security:EncryptionKey"] ?? Convert.ToBase64String(new byte[32])));

// Providers & Resolver
builder.Services.AddScoped<INotificationProvider, InAppProvider>();
builder.Services.AddScoped<INotificationProvider, SmtpEmailProvider>();
builder.Services.AddScoped<INotificationProvider, SignalRNotificationProvider>();
builder.Services.AddScoped<ProviderResolver>();
builder.Services.AddScoped<IProviderResolver>(sp => sp.GetRequiredService<ProviderResolver>());

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SubmitNotificationCommand).Assembly));

// Background processing with Hangfire (SQL Server or Memory fallback if credentials not yet configured)
var useMemory = builder.Configuration.GetValue<bool>("Hangfire:UseMemoryStorage");
bool isPlaceholderConfigured = connString.Contains("YOUR_SQL_USERNAME");

builder.Services.AddHangfire(cfg =>
{
    cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
       .UseSimpleAssemblyNameTypeSerializer()
       .UseRecommendedSerializerSettings();

    if (useMemory || isPlaceholderConfigured)
    {
        cfg.UseMemoryStorage();
    }
    else
    {
        try
        {
            cfg.UseSqlServerStorage(connString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            });
        }
        catch
        {
            cfg.UseMemoryStorage();
        }
    }
});

builder.Services.AddHangfireServer();
builder.Services.AddScoped<DeliveryDispatcher>();
builder.Services.AddScoped<OutboxProcessor>();

var app = builder.Build();

// Auto-create database & tables on startup when valid SQL Server connection is provided
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationGatewayDbContext>();
    if (!isPlaceholderConfigured)
    {
        try
        {
            db.Database.EnsureCreated();
            app.Logger.LogInformation("SQL Server database and tables ensured successfully.");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("SQL Server connection attempt: {Message}. Please verify your username and password in appsettings.json.", ex.Message);
        }
    }
    else
    {
        app.Logger.LogInformation("Database connection contains placeholder credentials. Set 'User Id' and 'Password' in appsettings.json to connect to your SQL Server.");
    }
}

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Multi-Tenant Notification Gateway API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

// Middleware order: Authentication -> TenantResolution -> Authorization
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// Redirect root to /swagger for instant access
app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// Recurring job for outbox processor
try
{
    app.UseHangfireDashboard("/hangfire");
    RecurringJob.AddOrUpdate<OutboxProcessor>(
        "process-outbox",
        p => p.ProcessAsync(CancellationToken.None),
        "*/10 * * * * *");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not initialize Hangfire dashboard or recurring job.");
}

app.Run();

public partial class Program { }
