using FactoryPlatform.Domain.Enums;

namespace FactoryPlatform.Domain.Entities;

public sealed class ProcessTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TagCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public Guid? EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public ProcessTagCategory Category { get; set; }
    public ProcessTagDataType DataType { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ProcessTagQuality Quality { get; set; } = ProcessTagQuality.Good;
    public ProcessTagSource Source { get; set; } = ProcessTagSource.Demo;
    public string PLCAddress { get; set; } = string.Empty;
    public int RefreshInterval { get; set; } = 5;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
