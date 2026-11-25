using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationGateway.API.Authentication;
using NotificationGateway.Application.Notifications.CancelNotification;
using NotificationGateway.Application.Notifications.GetNotification;
using NotificationGateway.Application.Notifications.SubmitNotification;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController(
    IMediator mediator,
    NotificationContext context,
    IDeliveryRepository deliveryRepo) : ControllerBase
{
    public record SubmitNotificationRequest(
        string Type,
        RecipientDto Recipient,
        IList<NotificationChannel> Channels,
        string Language,
        NotificationPriority Priority,
        JsonElement Payload,
        string? CorrelationId,
        JsonElement? Metadata,
        DateTime? ScheduledAt);

    [HttpPost]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitNotificationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (context.TenantId == Guid.Empty || context.ApplicationId == Guid.Empty)
            return Unauthorized(new { message = "Valid tenant and application context are required." });

        Guid? userId = Guid.TryParse(context.ClientId, out var parsedUid) ? parsedUid : null;

        var command = new SubmitNotificationCommand(
            context.TenantId,
            context.ApplicationId,
            request.Type,
            request.Recipient,
            request.Channels,
            request.Language ?? "en",
            request.Priority,
            request.Payload,
            idempotencyKey,
            request.CorrelationId,
            request.Metadata,
            request.ScheduledAt,
            userId);

        var result = await mediator.Send(command, ct);
        if (result.IsDuplicate)
            return Ok(result);

        return Accepted(result.StatusUrl, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNotificationQuery(context.TenantId, context.ApplicationId, id), ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var success = await mediator.Send(new CancelNotificationCommand(context.TenantId, context.ApplicationId, id), ct);
        if (!success) return NotFound();
        return Ok(new { message = "Notification cancellation requested." });
    }

    public record DeliveryAttemptDto(
        Guid Id,
        int AttemptNumber,
        string ProviderCode,
        string? ProviderMessageId,
        string Status,
        string FailureCategory,
        string? ErrorCode,
        string? SafeErrorMessage,
        bool IsRetryable,
        DateTime StartedAt,
        DateTime? CompletedAt,
        int? DurationMilliseconds);

    public record DeliveryDto(
        Guid Id,
        Guid NotificationId,
        string Channel,
        string? RecipientExternalUserId,
        string RecipientAddress,
        string Status,
        int AttemptCount,
        DateTime? NextAttemptAt,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        IReadOnlyList<DeliveryAttemptDto> Attempts);

    [HttpGet("{id:guid}/deliveries")]
    public async Task<IActionResult> GetDeliveries(Guid id, CancellationToken ct)
    {
        var deliveries = await deliveryRepo.GetByNotificationAsync(context.TenantId, id, ct);
        var dtos = deliveries.Select(d => new DeliveryDto(
            d.Id,
            d.NotificationId,
            d.Channel.ToString(),
            d.RecipientExternalUserId,
            d.RecipientAddress,
            d.Status.ToString(),
            d.AttemptCount,
            d.NextAttemptAt,
            d.CreatedAt,
            d.CompletedAt,
            d.Attempts.Select(a => new DeliveryAttemptDto(
                a.Id,
                a.AttemptNumber,
                a.ProviderCode,
                a.ProviderMessageId,
                a.Status.ToString(),
                a.FailureCategory.ToString(),
                a.ErrorCode,
                a.SafeErrorMessage,
                a.IsRetryable,
                a.StartedAt,
                a.CompletedAt,
                a.DurationMilliseconds)).ToList()
        )).ToList();

        return Ok(dtos);
    }
}
