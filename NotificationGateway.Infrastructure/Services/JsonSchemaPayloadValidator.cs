using NJsonSchema;
using NotificationGateway.Application.Abstractions;

namespace NotificationGateway.Infrastructure.Services;

public class JsonSchemaPayloadValidator : IPayloadValidator
{
    public async Task<PayloadValidationResult> ValidateAsync(string schemaJson, string payloadJson, CancellationToken ct)
    {
        try
        {
            var schema = await JsonSchema.FromJsonAsync(schemaJson, ct);
            var errors = schema.Validate(payloadJson);

            if (errors.Count == 0)
                return new PayloadValidationResult(true, Array.Empty<string>());

            var messages = errors.Select(e => $"{e.Path}: {e.Kind}").ToList();
            return new PayloadValidationResult(false, messages);
        }
        catch (Exception ex)
        {
            return new PayloadValidationResult(false, [$"Schema parse error: {ex.Message}"]);
        }
    }
}
