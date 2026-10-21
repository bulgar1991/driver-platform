using DriverPlatform.Domain.Entities;

namespace DriverPlatform.Application.Abstractions;

public interface ITokenService
{
    AccessToken CreateAccessToken(StaffUser user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
