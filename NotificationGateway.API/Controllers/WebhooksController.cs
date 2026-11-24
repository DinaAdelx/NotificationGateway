using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private static readonly ConcurrentBag<ReceivedWebhookEvent> _receivedEvents = new();

    public record ReceivedWebhookEvent(
        string Id,
        string EventId,
        string? SignatureHeader,
        bool IsSignatureValid,
        string? TimestampHeader,
        string? Attempt,
        DateTime ReceivedAt,
        object? Payload);

    /// <summary>
    /// Test webhook receiver endpoint simulating an external client enterprise application.
    /// Captures incoming webhooks with HMAC-SHA256 signature verification and payload logging.
    /// </summary>
    [HttpPost("receive-test")]
    public async Task<IActionResult> ReceiveTestWebhook(
        [FromHeader(Name = "X-Hub-Signature-256")] string? signature,
        [FromHeader(Name = "X-Webhook-Timestamp")] string? timestampHeader,
        [FromHeader(Name = "X-Event-ID")] string? eventId,
        [FromHeader(Name = "X-Delivery-Attempt")] string? attempt)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(rawBody))
            return BadRequest(new { error = "Request body is empty" });

        // Signature validation against default test secret key
        const string secretKey = "whsec_default_gateway_secret_2026!";
        bool isValidSignature = false;
        if (!string.IsNullOrWhiteSpace(signature))
        {
            var expectedSignature = "sha256=" + Convert.ToHexString(
                new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)).ComputeHash(Encoding.UTF8.GetBytes(rawBody))
            ).ToLowerInvariant();

            isValidSignature = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expectedSignature));
        }

        object? parsedPayload;
        try
        {
            parsedPayload = JsonSerializer.Deserialize<JsonElement>(rawBody);
        }
        catch
        {
            parsedPayload = rawBody;
        }

        var entry = new ReceivedWebhookEvent(
            Id: Guid.NewGuid().ToString("N"),
            EventId: eventId ?? "unknown",
            SignatureHeader: signature,
            IsSignatureValid: isValidSignature,
            TimestampHeader: timestampHeader,
            Attempt: attempt ?? "1",
            ReceivedAt: DateTime.UtcNow,
            Payload: parsedPayload);

        _receivedEvents.Add(entry);

        return Ok(new
        {
            status = "accepted",
            message = "Webhook received and verified successfully.",
            receivedAt = DateTime.UtcNow,
            eventId,
            signatureValid = isValidSignature
        });
    }

    /// <summary>Returns recent test webhooks received by this gateway.</summary>
    [HttpGet("received")]
    public IActionResult GetReceivedWebhooks([FromQuery] int limit = 50)
    {
        var items = _receivedEvents
            .OrderByDescending(e => e.ReceivedAt)
            .Take(limit)
            .ToList();

        return Ok(items);
    }

    /// <summary>Clears the recorded test webhooks.</summary>
    [HttpPost("clear")]
    public IActionResult ClearReceivedWebhooks()
    {
        _receivedEvents.Clear();
        return Ok(new { message = "Received webhook history cleared." });
    }
}
