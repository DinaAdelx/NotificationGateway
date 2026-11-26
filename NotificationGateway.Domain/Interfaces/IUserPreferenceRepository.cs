using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Domain.Interfaces;

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetByExternalUserIdAsync(Guid tenantId, Guid applicationId, string externalUserId, CancellationToken ct = default);
    Task AddAsync(UserPreference preference, CancellationToken ct = default);
    Task UpdateAsync(UserPreference preference, CancellationToken ct = default);
}
