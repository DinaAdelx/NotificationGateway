using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Infrastructure.Persistence;

namespace NotificationGateway.Infrastructure.Persistence;

public class UnitOfWork(NotificationGatewayDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
