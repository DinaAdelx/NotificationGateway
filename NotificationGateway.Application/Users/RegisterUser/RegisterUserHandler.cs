using MediatR;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Application.Users.RegisterUser;

public record RegisterUserCommand(
    Guid TenantId,
    Guid ApplicationId,
    string Email,
    string Password,
    string FullName,
    string Role = "Customer") : IRequest<RegisterUserResult>;

public record RegisterUserResult(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Token);

public class RegisterUserHandler(
    IUserRepository userRepo,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IJwtTokenService jwtService)
    : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        if (await userRepo.ExistsByEmailAsync(request.TenantId, request.ApplicationId, request.Email, ct))
        {
            throw new InvalidOperationException($"User with email '{request.Email}' already exists in this tenant/application.");
        }

        var passwordHash = hasher.HashPassword(request.Password);
        var user = User.Create(
            request.TenantId,
            request.ApplicationId,
            request.Email,
            passwordHash,
            request.FullName,
            request.Role);

        await userRepo.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        var token = jwtService.GenerateToken(user);
        return new RegisterUserResult(user.Id, user.Email, user.FullName, user.Role, token);
    }
}
