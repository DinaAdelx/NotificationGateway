using Microsoft.EntityFrameworkCore;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Infrastructure.Persistence.Repositories;

public class UserPreferenceRepository(NotificationGatewayDbContext db) : IUserPreferenceRepository
{
    public Task<UserPreference?> GetByExternalUserIdAsync(Guid tenantId, Guid applicationId, string externalUserId, CancellationToken ct = default)
        => db.UserPreferences.FirstOrDefaultAsync(p =>
            p.TenantId == tenantId && p.ApplicationId == applicationId && p.ExternalUserId == externalUserId, ct);

    public async Task AddAsync(UserPreference preference, CancellationToken ct = default)
        => await db.UserPreferences.AddAsync(preference, ct);

    public Task UpdateAsync(UserPreference preference, CancellationToken ct = default)
    {
        db.UserPreferences.Update(preference);
        return Task.CompletedTask;
    }
}
