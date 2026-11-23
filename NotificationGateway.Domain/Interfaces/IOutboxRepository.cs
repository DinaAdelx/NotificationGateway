using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Domain.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(NotificationOutbox entry, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationOutbox>> GetPendingAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid id, CancellationToken ct = default);
}
