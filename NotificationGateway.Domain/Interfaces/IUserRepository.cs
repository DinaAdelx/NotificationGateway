using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Guid tenantId, Guid applicationId, string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Guid tenantId, Guid applicationId, string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
