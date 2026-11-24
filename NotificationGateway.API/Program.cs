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
using NotificationGateway.Infrastructure.Providers.Sms;
using NotificationGateway.Infrastructure.Providers.WhatsApp;
using NotificationGateway.Infrastructure.Providers.Webhook;
using NotificationGateway.Infrastructure.Security;
using NotificationGateway.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers & SignalR
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
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
    ?? builder.Configuration.GetConnectionString("NotificationGatewayDb")
    ?? "Server=localhost;Database=NotificationGatewayDb;User Id=YOUR_SQL_USERNAME;Password=YOUR_SQL_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<NotificationGatewayDbContext>(opts =>
{
    opts.UseSqlServer(connString, sqlOpts =>
    {
        sqlOpts.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
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

// HTTP Client for providers & webhooks
builder.Services.AddHttpClient();

// Providers & Resolver
builder.Services.AddScoped<INotificationProvider, InAppProvider>();
builder.Services.AddScoped<INotificationProvider, SmtpEmailProvider>();
builder.Services.AddScoped<INotificationProvider, SignalRNotificationProvider>();
builder.Services.AddScoped<INotificationProvider, SmsNotificationProvider>();
builder.Services.AddScoped<INotificationProvider, WhatsAppNotificationProvider>();
builder.Services.AddScoped<INotificationProvider, WebhookNotificationProvider>();
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

// Automatically apply EF Core migrations and seed default Tenant/Application on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationGatewayDbContext>();
    if (!isPlaceholderConfigured)
    {
        try
        {
            db.Database.Migrate();
            app.Logger.LogInformation("SQL Server database and all entity tables migrated/created successfully.");

            // Seed default test tenant and application client if they don't exist
            var defaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var defaultAppId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            if (!db.Tenants.Any(t => t.Id == defaultTenantId))
            {
                var defaultTenant = NotificationGateway.Domain.Entities.Tenant.Create("Default Tenant", "default-tenant");
                typeof(NotificationGateway.Domain.Entities.Tenant).GetProperty("Id")!.SetValue(defaultTenant, defaultTenantId);
                db.Tenants.Add(defaultTenant);

                var defaultApp = NotificationGateway.Domain.Entities.ApplicationClient.Create(
                    defaultTenantId,
                    "Default Client App",
                    "order-service",
                    "hashed-api-key-sample",
                    "prefix");
                typeof(NotificationGateway.Domain.Entities.ApplicationClient).GetProperty("Id")!.SetValue(defaultApp, defaultAppId);
                db.ApplicationClients.Add(defaultApp);

                db.SaveChanges();
                app.Logger.LogInformation("Default Tenant ({TenantId}) and ApplicationClient ({AppId}) seeded successfully.", defaultTenantId, defaultAppId);
            }

            // Seed default provider configurations for Email, SMS, WhatsApp, SignalR, Webhook
            if (!db.TenantProviderConfigurations.Any(c => c.TenantId == defaultTenantId))
            {
                db.TenantProviderConfigurations.AddRange(
                    NotificationGateway.Domain.Entities.TenantProviderConfiguration.Create(
                        defaultTenantId, defaultAppId, "SMTP", NotificationGateway.Domain.Enums.NotificationChannel.Email,
                        "{\"Host\":\"mock\",\"Port\":587,\"UseSsl\":false,\"FromEmail\":\"noreply@gateway.local\",\"FromName\":\"Notification Gateway\"}",
                        null, true, 1),
                    NotificationGateway.Domain.Entities.TenantProviderConfiguration.Create(
                        defaultTenantId, defaultAppId, "SIGNALR", NotificationGateway.Domain.Enums.NotificationChannel.SignalR,
                        "{}", null, true, 1),
                    NotificationGateway.Domain.Entities.TenantProviderConfiguration.Create(
                        defaultTenantId, defaultAppId, "SMS", NotificationGateway.Domain.Enums.NotificationChannel.Sms,
                        "{\"SenderId\":\"Gateway\"}", null, true, 1),
                    NotificationGateway.Domain.Entities.TenantProviderConfiguration.Create(
                        defaultTenantId, defaultAppId, "WHATSAPP", NotificationGateway.Domain.Enums.NotificationChannel.WhatsApp,
                        "{}", null, true, 1),
                    NotificationGateway.Domain.Entities.TenantProviderConfiguration.Create(
                        defaultTenantId, defaultAppId, "WEBHOOK", NotificationGateway.Domain.Enums.NotificationChannel.Webhook,
                        "{\"SecretKey\":\"whsec_default_gateway_secret_2026!\"}", null, true, 1)
                );
                db.SaveChanges();
                app.Logger.LogInformation("Default Provider configurations for Email, SMS, WhatsApp, SignalR, and Webhook seeded successfully.");
            }

            // Seed default NotificationType "ORDER_SHIPPED" and an active template
            var defaultNotifType = db.NotificationTypeDefinitions.FirstOrDefault(t => t.TenantId == defaultTenantId && t.Code == "ORDER_SHIPPED");
            if (defaultNotifType == null)
            {
                defaultNotifType = NotificationGateway.Domain.Entities.NotificationTypeDefinition.Create(
                    defaultTenantId, defaultAppId, "ORDER_SHIPPED", "Order Shipped Notification",
                    "{\"type\":\"object\",\"properties\":{\"customerName\":{\"type\":\"string\"},\"orderId\":{\"type\":\"string\"},\"carrier\":{\"type\":\"string\"},\"trackingUrl\":{\"type\":\"string\"}},\"required\":[\"customerName\",\"orderId\"]}",
                    new[] {
                        NotificationGateway.Domain.Enums.NotificationChannel.Email,
                        NotificationGateway.Domain.Enums.NotificationChannel.Sms,
                        NotificationGateway.Domain.Enums.NotificationChannel.WhatsApp,
                        NotificationGateway.Domain.Enums.NotificationChannel.SignalR,
                        NotificationGateway.Domain.Enums.NotificationChannel.Webhook
                    });
                db.NotificationTypeDefinitions.Add(defaultNotifType);
                db.SaveChanges();

                var defaultTemplate = NotificationGateway.Domain.Entities.NotificationTemplate.Create(
                    defaultTenantId, defaultAppId, defaultNotifType.Id, NotificationGateway.Domain.Enums.NotificationChannel.Email,
                    "en",
                    "{\"content\":\"<h1>Order Shipped</h1><p>Hi {{customerName}},</p><p>Your order <strong>{{orderId}}</strong> has been dispatched via {{carrier}}.</p><p><a href=\\\"{{trackingUrl}}\\\">Track Package</a></p>\"}",
                    "Great news, {{customerName}}! Your order {{orderId}} is on the way",
                    new[] { "customerName", "orderId", "carrier", "trackingUrl" });
                defaultTemplate.Publish();
                db.NotificationTemplates.Add(defaultTemplate);
                db.SaveChanges();
                app.Logger.LogInformation("Default ORDER_SHIPPED NotificationType and Active Template seeded successfully.");
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("SQL Server migration attempt: {Message}. Please verify your username and password in appsettings.json.", ex.Message);
        }
    }
    else
    {
        app.Logger.LogInformation("Database connection contains placeholder credentials. Set 'User Id' and 'Password' in appsettings.json to connect to your SQL Server.");
    }
}

// Serve testing UI from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable Swagger UI (kept accessible at /swagger)
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
