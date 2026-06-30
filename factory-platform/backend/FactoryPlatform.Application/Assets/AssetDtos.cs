using FactoryPlatform.Domain.Enums;

namespace FactoryPlatform.Application.Assets;

public sealed record AssetDto(
    Guid Id,
    string Code,
    string Name,
    AssetCategory Category,
    string Area,
    string Manufacturer,
    string Model,
    string SerialNumber,
    AssetStatus Status,
    string Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpsertAssetRequest(
    string Code,
    string Name,
    AssetCategory Category,
    string Area,
    string Manufacturer,
    string Model,
    string SerialNumber,
    AssetStatus Status,
    string Notes);
