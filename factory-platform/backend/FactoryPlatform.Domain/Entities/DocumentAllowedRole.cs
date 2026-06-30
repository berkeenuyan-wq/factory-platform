namespace FactoryPlatform.Domain.Entities;

public sealed class DocumentAllowedRole
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
