namespace FactoryPlatform.Application.Users;

public sealed record RoleDto(Guid Id, string Name);
public sealed record UserMeDto(Guid Id, string Email, string DisplayName, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);
