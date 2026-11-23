using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        b.Property(u => u.Role).HasMaxLength(50).IsRequired();
        b.HasIndex(u => new { u.TenantId, u.ApplicationId, u.Email }).IsUnique();

        b.HasMany(u => u.Templates)
         .WithOne(t => t.User)
         .HasForeignKey(t => t.UserId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}
