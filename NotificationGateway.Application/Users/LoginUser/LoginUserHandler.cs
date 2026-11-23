using MediatR;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Application.Users.LoginUser;

public record LoginUserCommand(
    Guid TenantId,
    Guid ApplicationId,
    string Email,
    string Password) : IRequest<LoginUserResult>;

public record LoginUserResult(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Token);

public class LoginUserHandler(
    IUserRepository userRepo,
    IPasswordHasher hasher,
    IJwtTokenService jwtService)
    : IRequestHandler<LoginUserCommand, LoginUserResult>
{
    public async Task<LoginUserResult> Handle(LoginUserCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByEmailAsync(request.TenantId, request.ApplicationId, request.Email, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("This user account is inactive.");

        if (!hasher.VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var token = jwtService.GenerateToken(user);
        return new LoginUserResult(user.Id, user.Email, user.FullName, user.Role, token);
    }
}
