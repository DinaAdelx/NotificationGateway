using Microsoft.EntityFrameworkCore;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Infrastructure.Persistence.Repositories;

public class UserRepository(NotificationGatewayDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);

    public Task<User?> GetByEmailAsync(Guid tenantId, Guid applicationId, string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u =>
            u.TenantId == tenantId &&
            u.ApplicationId == applicationId &&
            u.Email == email.Trim().ToLowerInvariant(), ct);

    public Task<bool> ExistsByEmailAsync(Guid tenantId, Guid applicationId, string email, CancellationToken ct = default) =>
        db.Users.AnyAsync(u =>
            u.TenantId == tenantId &&
            u.ApplicationId == applicationId &&
            u.Email == email.Trim().ToLowerInvariant(), ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        return Task.CompletedTask;
    }
}
