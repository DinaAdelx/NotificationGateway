using FluentAssertions;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Infrastructure.Security;
using NotificationGateway.Infrastructure.Services;
using Xunit;

namespace NotificationGateway.Tests.Unit;

public class PayloadValidatorTests
{
    private readonly JsonSchemaPayloadValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_ValidPayload_ReturnsTrue()
    {
        var schema = """
        {
          "type": "object",
          "required": ["orderId", "customerName", "total"],
          "properties": {
            "orderId": { "type": "string" },
            "customerName": { "type": "string" },
            "total": { "type": "number" }
          }
        }
        """;

        var payload = """
        {
          "orderId": "ORD-100",
          "customerName": "Ahmed",
          "total": 500.0
        }
        """;

        var result = await _validator.ValidateAsync(schema, payload, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_MissingRequiredProperty_ReturnsFalse()
    {
        var schema = """
        {
          "type": "object",
          "required": ["orderId", "customerName"]
        }
        """;

        var payload = """
        {
          "orderId": "ORD-100"
        }
        """;

        var result = await _validator.ValidateAsync(schema, payload, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}

public class TemplateRendererTests
{
    private readonly HandlebarsTemplateRenderer _renderer = new(null!);

    [Fact]
    public void Preview_RendersPlaceholdersProperly()
    {
        var content = """{"content": "Hello {{customerName}}, your order {{orderId}} is confirmed!"}""";
        var payload = """{"customerName": "Dina", "orderId": "ORD-999"}""";

        var result = _renderer.Preview(content, "Subject {{orderId}}", payload);

        result.Body.Should().Be("Hello Dina, your order ORD-999 is confirmed!");
        result.Subject.Should().Be("Subject ORD-999");
    }
}

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_GeneratesSaltedHash_AndVerifiesCorrectly()
    {
        var rawPassword = "SuperSecurePassword123!";
        var hash = _hasher.HashPassword(rawPassword);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Contain("."); // Salt and hash separator

        var isValid = _hasher.VerifyPassword(rawPassword, hash);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("CorrectPassword123!");
        var isValid = _hasher.VerifyPassword("WrongPassword!", hash);

        isValid.Should().BeFalse();
    }
}

public class MultiProviderTests
{
    [Fact]
    public void WebhookSignature_GeneratesValidHmacSha256()
    {
        var payload = """{"event": "order.shipped", "orderId": "ORD-123"}""";
        var secret = "whsec_test_secret_123!";

        var signature = NotificationGateway.Infrastructure.Providers.Webhook.WebhookNotificationProvider.ComputeHmacSha256(payload, secret);

        signature.Should().StartWith("sha256=");
        signature.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task SmsProvider_SimulatedSend_ReturnsSuccessAndId()
    {
        var loggerMock = new Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationGateway.Infrastructure.Providers.Sms.SmsNotificationProvider>();
        var emptyConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var provider = new NotificationGateway.Infrastructure.Providers.Sms.SmsNotificationProvider(new HttpClient(), emptyConfig, loggerMock);
        var config = NotificationGateway.Domain.Entities.TenantProviderConfiguration.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SMS", NotificationGateway.Domain.Enums.NotificationChannel.Sms, "{}");

        var notif = new RenderedNotification(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            NotificationGateway.Domain.Enums.NotificationChannel.Sms,
            "+1234567890", "Subject", "Your order has shipped", null);

        var result = await provider.SendAsync(notif, config, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().StartWith("SM");
    }

    [Fact]
    public async Task WhatsAppProvider_SimulatedSend_ReturnsSuccessAndWamid()
    {
        var loggerMock = new Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationGateway.Infrastructure.Providers.WhatsApp.WhatsAppNotificationProvider>();
        // Pass a no-op IConfiguration so the provider falls into simulation mode (no real credentials)
        var emptyConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var provider = new NotificationGateway.Infrastructure.Providers.WhatsApp.WhatsAppNotificationProvider(new HttpClient(), emptyConfig, loggerMock);
        var config = NotificationGateway.Domain.Entities.TenantProviderConfiguration.Create(
            Guid.NewGuid(), Guid.NewGuid(), "WHATSAPP", NotificationGateway.Domain.Enums.NotificationChannel.WhatsApp, "{}");

        var notif = new RenderedNotification(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            NotificationGateway.Domain.Enums.NotificationChannel.WhatsApp,
            "+1234567890", null, "Hello from WhatsApp", null);

        var result = await provider.SendAsync(notif, config, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ProviderMessageId.Should().StartWith("wamid.");
    }

    [Fact]
    public void HandlebarsRenderer_WebhookJsonTemplate_RendersCleanJson()
    {
        var repo = new InMemoryTemplateRepoStub();
        var renderer = new NotificationGateway.Infrastructure.Services.HandlebarsTemplateRenderer(repo);

        var webhookContent = "{\"event\":\"ORDER_SHIPPED\",\"orderId\":\"{{orderId}}\",\"customerName\":\"{{customerName}}\" }";
        var payload = "{\"orderId\":\"ORD-99\",\"customerName\":\"Dina\"}";

        var rendered = renderer.Preview(webhookContent, "ORDER_SHIPPED", payload);

        rendered.Body.Should().Contain("\"orderId\":\"ORD-99\"");
        rendered.Body.Should().Contain("\"customerName\":\"Dina\"");
    }

    private class InMemoryTemplateRepoStub : NotificationGateway.Domain.Interfaces.ITemplateRepository
    {
        public Task<NotificationGateway.Domain.Entities.NotificationTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => Task.FromResult<NotificationGateway.Domain.Entities.NotificationTemplate?>(null);
        public Task<NotificationGateway.Domain.Entities.NotificationTemplate?> GetActiveAsync(Guid tenantId, Guid applicationId, Guid notificationTypeId, NotificationGateway.Domain.Enums.NotificationChannel channel, string language, CancellationToken ct = default) => Task.FromResult<NotificationGateway.Domain.Entities.NotificationTemplate?>(null);
        public Task<IReadOnlyList<NotificationGateway.Domain.Entities.NotificationTemplate>> GetAllAsync(Guid tenantId, Guid applicationId, Guid? userId = null, Guid? notificationTypeId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<NotificationGateway.Domain.Entities.NotificationTemplate>>(Array.Empty<NotificationGateway.Domain.Entities.NotificationTemplate>());
        public Task AddAsync(NotificationGateway.Domain.Entities.NotificationTemplate template, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(NotificationGateway.Domain.Entities.NotificationTemplate template, CancellationToken ct = default) => Task.CompletedTask;
    }
}
