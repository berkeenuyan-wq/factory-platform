using FactoryPlatform.Application.Users;
using FactoryPlatform.Domain.Entities;

namespace FactoryPlatform.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateToken(User user, IReadOnlyCollection<RoleDto> roles, IReadOnlyCollection<string> permissions);
}
