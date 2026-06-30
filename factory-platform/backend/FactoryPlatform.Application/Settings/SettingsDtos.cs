namespace FactoryPlatform.Application.Settings;

public sealed record UserSettingsDto(Guid Id, Guid UserId, string SettingsJson);
public sealed record UpdateUserSettingsRequest(string SettingsJson);
