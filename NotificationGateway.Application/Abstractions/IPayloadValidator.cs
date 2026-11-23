namespace NotificationGateway.Application.Abstractions;

public interface IPayloadValidator
{
    /// <summary>Validates a JSON payload string against a JSON Schema string.</summary>
    Task<PayloadValidationResult> ValidateAsync(string schemaJson, string payloadJson, CancellationToken ct = default);
}

public record PayloadValidationResult(bool IsValid, IReadOnlyList<string> Errors);
