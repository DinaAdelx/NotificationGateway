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
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
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
builder.Services.AddHttpClient<SmsNotificationProvider>();
builder.Services.AddHttpClient<WebhookNotificationProvider>();
builder.Services.AddHttpClient<WhatsAppNotificationProvider>();

// Providers & Resolver
builder.Services.AddScoped<INotificationProvider, InAppProvider>();
builder.Services.AddScoped<INotificationProvider, SmtpEmailProvider>();
builder.Services.AddScoped<INotificationProvider, SignalRNotificationProvider>();
// SMS, WhatsApp, Webhook use typed HttpClient — register via factory so DI injects the typed client
builder.Services.AddScoped<INotificationProvider>(sp =>
    new SmsNotificationProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SmsNotificationProvider)),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<SmsNotificationProvider>>()));
builder.Services.AddScoped<INotificationProvider>(sp =>
    new WhatsAppNotificationProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WhatsAppNotificationProvider)),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<WhatsAppNotificationProvider>>()));
builder.Services.AddScoped<INotificationProvider>(sp =>
    new WebhookNotificationProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WebhookNotificationProvider)),
        sp.GetRequiredService<ILogger<WebhookNotificationProvider>>()));
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

            // Ensure per-user UserId columns exist for dynamic per-user provider settings & notifications
            db.Database.ExecuteSqlRaw(@"
                IF COL_LENGTH('TenantProviderConfigurations', 'UserId') IS NULL
                    ALTER TABLE TenantProviderConfigurations ADD UserId UNIQUEIDENTIFIER NULL;
                IF COL_LENGTH('Notifications', 'UserId') IS NULL
                    ALTER TABLE Notifications ADD UserId UNIQUEIDENTIFIER NULL;");

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

            // Remove any legacy seeded tenant-wide default provider configurations (UserId IS NULL)
            // so every user manages their own dynamic settings without pre-filled defaults.
            db.Database.ExecuteSqlRaw("DELETE FROM TenantProviderConfigurations WHERE UserId IS NULL;");

            // Seed default NotificationType "ORDER_SHIPPED" and channel-specific active templates
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
                app.Logger.LogInformation("Default ORDER_SHIPPED NotificationType seeded successfully.");
            }

            // Ensure each channel has its own tailored active template for ORDER_SHIPPED
            var vars = new[] { "customerName", "orderId", "carrier", "trackingUrl" };
            var channelTemplates = new (NotificationGateway.Domain.Enums.NotificationChannel Channel, string ContentJson, string? Subject)[]
            {
                (
                    NotificationGateway.Domain.Enums.NotificationChannel.Email,
                    "{\"content\":\"<h1>Order Shipped</h1><p>Hi {{customerName}},</p><p>Your order <strong>{{orderId}}</strong> has been dispatched via {{carrier}}.</p><p><a href=\\\"{{trackingUrl}}\\\">Track Package</a></p>\"}",
                    "Great news, {{customerName}}! Your order {{orderId}} is on the way"
                ),
                (
                    NotificationGateway.Domain.Enums.NotificationChannel.Sms,
                    "{\"message\":\"Hi {{customerName}}, your order {{orderId}} shipped via {{carrier}}. Track: {{trackingUrl}}\"}",
                    null
                ),
                (
                    NotificationGateway.Domain.Enums.NotificationChannel.WhatsApp,
                    "{\"message\":\"*Order Shipped!* 🚚\\nHi {{customerName}},\\nYour order *{{orderId}}* has been dispatched via {{carrier}}.\\nTrack your package: {{trackingUrl}}\"}",
                    null
                ),
                (
                    NotificationGateway.Domain.Enums.NotificationChannel.SignalR,
                    "{\"message\":\"Order {{orderId}} for {{customerName}} has been dispatched via {{carrier}}.\"}",
                    "Order {{orderId}} Shipped"
                ),
                (
                    NotificationGateway.Domain.Enums.NotificationChannel.Webhook,
                    "{\"event\":\"ORDER_SHIPPED\",\"orderId\":\"{{orderId}}\",\"customerName\":\"{{customerName}}\",\"carrier\":\"{{carrier}}\",\"trackingUrl\":\"{{trackingUrl}}\" }",
                    "ORDER_SHIPPED"
                )
            };

            var existingChannels = db.NotificationTemplates
                .Where(t => t.TenantId == defaultTenantId
                         && t.NotificationTypeDefinitionId == defaultNotifType.Id
                         && t.Status == NotificationGateway.Domain.Enums.TemplateStatus.Active)
                .Select(t => t.Channel)
                .ToHashSet();

            var seededAnyTemplate = false;
            foreach (var (channel, contentJson, subject) in channelTemplates)
            {
                if (!existingChannels.Contains(channel))
                {
                    var tpl = NotificationGateway.Domain.Entities.NotificationTemplate.Create(
                        defaultTenantId, defaultAppId, defaultNotifType.Id, channel, "en", contentJson, subject, vars);
                    tpl.Publish();
                    db.NotificationTemplates.Add(tpl);
                    seededAnyTemplate = true;
                }
            }

            if (seededAnyTemplate)
            {
                db.SaveChanges();
                app.Logger.LogInformation("Channel-specific ORDER_SHIPPED templates seeded successfully.");
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
