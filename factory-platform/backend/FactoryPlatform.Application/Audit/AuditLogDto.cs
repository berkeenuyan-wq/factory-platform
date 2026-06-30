namespace FactoryPlatform.Application.Audit;

public sealed record AuditLogDto(Guid Id, string Action, string EntityName, string? EntityId, Guid? UserId, DateTime CreatedAtUtc);
