using FactoryPlatform.Domain.Enums;

namespace FactoryPlatform.Application.Documents;

public sealed record DocumentDto(
    Guid Id,
    string Title,
    string Description,
    DocumentType DocumentType,
    string Revision,
    Guid? AssetId,
    string? AssetCode,
    string? AssetName,
    string FileName,
    string OriginalFileName,
    string FileExtension,
    long FileSize,
    Guid UploadedBy,
    string? UploadedByName,
    DateTime UploadedAt,
    DateTime LastModified,
    DocumentStatus Status,
    DocumentVisibility Visibility,
    RoleOptionDto[] AllowedRoles);

public sealed record DocumentListResponse(DocumentDto[] Items, int TotalCount, int Page, int PageSize);

public sealed record DocumentUploadRequest(
    string Title,
    string Description,
    DocumentType DocumentType,
    string Revision,
    Guid? AssetId,
    DocumentStatus Status,
    DocumentVisibility Visibility,
    Guid[] AllowedRoleIds);

public sealed record UpdateDocumentRequest(
    string Title,
    string Description,
    DocumentType DocumentType,
    string Revision,
    Guid? AssetId,
    DocumentStatus Status,
    DocumentVisibility Visibility,
    Guid[] AllowedRoleIds);

public sealed record RoleOptionDto(Guid Id, string Name);
