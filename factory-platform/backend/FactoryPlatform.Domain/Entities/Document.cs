using FactoryPlatform.Domain.Enums;

namespace FactoryPlatform.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string Revision { get; set; } = string.Empty;
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public Guid UploadedBy { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DocumentVisibility Visibility { get; set; } = DocumentVisibility.Public;
    public ICollection<DocumentAllowedRole> AllowedRoles { get; set; } = new List<DocumentAllowedRole>();
}
