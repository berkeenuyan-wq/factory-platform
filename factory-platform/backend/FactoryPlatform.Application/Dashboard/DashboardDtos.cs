namespace FactoryPlatform.Application.Dashboard;

public sealed record DashboardLayoutDto(Guid Id, Guid UserId, string Name, string LayoutJson, DateTime UpdatedAtUtc);
public sealed record CreateDashboardLayoutRequest(string Name, string LayoutJson);
public sealed record UpdateDashboardLayoutRequest(string Name, string LayoutJson);
