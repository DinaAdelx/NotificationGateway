using FluentAssertions;
using NotificationGateway.Application.Abstractions;
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
