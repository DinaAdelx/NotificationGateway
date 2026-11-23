using MediatR;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Application.Abstractions;
using System.Text.Json;

namespace NotificationGateway.Application.Notifications.SubmitNotification;

public record SubmitNotificationCommand(
    Guid TenantId,
    Guid ApplicationId,
    string Type,
    RecipientDto Recipient,
    IList<NotificationChannel> Channels,
    string Language,
    NotificationPriority Priority,
    JsonElement Payload,
    string? IdempotencyKey,
    string? CorrelationId,
    JsonElement? Metadata,
    DateTime? ScheduledAt) : IRequest<SubmitNotificationResult>;

public record RecipientDto(
    string? ExternalUserId,
    string? Email,
    string? Phone,
    string? DeviceToken);

public record SubmitNotificationResult(
    Guid NotificationId,
    string Status,
    DateTime AcceptedAt,
    string StatusUrl,
    bool IsDuplicate = false);

public class SubmitNotificationHandler(
    INotificationTypeRepository typeRepo,
    INotificationRepository notificationRepo,
    IOutboxRepository outboxRepo,
    IUnitOfWork uow,
    IPayloadValidator payloadValidator)
    : IRequestHandler<SubmitNotificationCommand, SubmitNotificationResult>
{
    public async Task<SubmitNotificationResult> Handle(SubmitNotificationCommand request, CancellationToken ct)
    {
        // 1. Check idempotency
        if (request.IdempotencyKey is not null)
        {
            var existing = await notificationRepo.GetByIdempotencyKeyAsync(
                request.TenantId, request.ApplicationId, request.IdempotencyKey, ct);
            if (existing is not null)
                return new SubmitNotificationResult(
                    existing.Id, existing.Status.ToString(), existing.CreatedAt,
                    $"/api/v1/notifications/{existing.Id}", IsDuplicate: true);
        }

        // 2. Resolve notification type
        var notifType = await typeRepo.GetByCodeAsync(request.TenantId, request.ApplicationId, request.Type, ct)
            ?? throw new InvalidOperationException($"Notification type '{request.Type}' not found or inactive.");

        if (!notifType.IsActive)
            throw new InvalidOperationException($"Notification type '{request.Type}' is inactive.");

        // 3. Validate payload
        var payloadJson = request.Payload.GetRawText();
        var validation = await payloadValidator.ValidateAsync(notifType.PayloadSchemaJson, payloadJson, ct);
        if (!validation.IsValid)
            throw new ArgumentException($"Payload validation failed: {string.Join("; ", validation.Errors)}");

        // 4. Create notification
        var notification = Domain.Entities.Notification.Create(
            request.TenantId, request.ApplicationId,
            notifType.Id, notifType.Code, notifType.SchemaVersion,
            payloadJson, request.Priority,
            request.IdempotencyKey, request.CorrelationId,
            request.Metadata?.GetRawText(), request.ScheduledAt);

        await notificationRepo.AddAsync(notification, ct);

        // 5. Outbox entry (same transaction)
        var outbox = Domain.Entities.NotificationOutbox.Create(
            request.TenantId, notification.Id,
            "NotificationAccepted",
            JsonSerializer.Serialize(new { notification.Id, request.Channels, request.Language, request.Recipient }));

        await outboxRepo.AddAsync(outbox, ct);
        await uow.SaveChangesAsync(ct);

        return new SubmitNotificationResult(
            notification.Id, notification.Status.ToString(),
            notification.CreatedAt, $"/api/v1/notifications/{notification.Id}");
    }
}
