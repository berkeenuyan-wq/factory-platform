using FactoryPlatform.Domain.Enums;

namespace FactoryPlatform.Application.ProcessTags;

public sealed record ProcessTagDto(
    Guid Id,
    string TagCode,
    string Name,
    string Area,
    Guid? EquipmentId,
    string EquipmentName,
    ProcessTagCategory Category,
    ProcessTagDataType DataType,
    string Unit,
    string Value,
    ProcessTagQuality Quality,
    ProcessTagSource Source,
    string PLCAddress,
    int RefreshInterval,
    DateTime LastUpdated,
    bool IsActive);

public sealed record WaterTreatmentTankDto(string Name, string TagCode, decimal Level, string Unit, string Status, DateTime LastUpdated);

public sealed record WaterTreatmentMetricDto(string Label, string TagCode, string Value, string Unit, ProcessTagQuality Quality, DateTime LastUpdated);

public sealed record WaterTreatmentWidgetDto(
    string Area,
    DateTime LastUpdated,
    WaterTreatmentTankDto[] Tanks,
    WaterTreatmentMetricDto FeedPump1Status,
    WaterTreatmentMetricDto FeedPump2Status,
    WaterTreatmentMetricDto RoPressure,
    WaterTreatmentMetricDto RoConductivity,
    WaterTreatmentMetricDto RoFlow,
    WaterTreatmentMetricDto SoftWaterFlow,
    WaterTreatmentMetricDto ActiveAlarms);
