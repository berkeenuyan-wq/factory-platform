namespace FactoryPlatform.Application.Common;

public static class SeedData
{
    public static readonly (string Name, string Description)[] Roles =
    [
        ("Admin", "Platform administrator"),
        ("Administrator", "FactoryOS administrator"),
        ("Engineer", "Engineering team"),
        ("Maintenance", "Maintenance team"),
        ("Production", "Production team"),
        ("Quality", "Quality team"),
        ("Warehouse", "Warehouse team"),
        ("Management", "Management team"),
        ("Guest", "Limited read-only access")
    ];

    public static readonly string[] PermissionKeys =
    [
        "dashboard.view",
        "dashboard.edit",
        "assets.view",
        "assets.edit",
        "commissioning.view",
        "maintenance.view",
        "warehouse.view",
        "documents.view",
        "documents.upload",
        "documents.edit",
        "documents.delete",
        "scada.view",
        "reports.view",
        "settings.view",
        "audit.view"
    ];
}
