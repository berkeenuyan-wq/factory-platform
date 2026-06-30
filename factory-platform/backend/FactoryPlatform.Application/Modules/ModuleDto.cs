namespace FactoryPlatform.Application.Modules;

public sealed record ModuleDto(string Key, string Name, string Route, string Icon, IReadOnlyCollection<string> Permissions, int Order);
