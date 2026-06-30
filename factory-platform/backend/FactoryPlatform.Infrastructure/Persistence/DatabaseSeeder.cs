using FactoryPlatform.Application.Abstractions;
using FactoryPlatform.Application.Common;
using FactoryPlatform.Domain.Entities;
using FactoryPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactoryPlatform.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var permissions = new Dictionary<string, Permission>();
        foreach (var key in SeedData.PermissionKeys)
        {
            var permission = await dbContext.Permissions.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (permission is null)
            {
                permission = new Permission { Key = key, Description = key };
                dbContext.Permissions.Add(permission);
            }
            permissions[key] = permission;
        }

        foreach (var roleSeed in SeedData.Roles)
        {
            if (!await dbContext.Roles.AnyAsync(x => x.Name == roleSeed.Name, cancellationToken))
            {
                dbContext.Roles.Add(new Role { Name = roleSeed.Name, Description = roleSeed.Description });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var adminRole = await dbContext.Roles
            .Include(x => x.RolePermissions)
            .FirstAsync(x => x.Name == "Admin", cancellationToken);

        adminRole.Description = "Platform administrator";

        foreach (var permission in permissions.Values)
        {
            if (adminRole.RolePermissions.All(x => x.PermissionId != permission.Id))
            {
                adminRole.RolePermissions.Add(new RolePermission { Role = adminRole, Permission = permission });
            }
        }

        var admin = await dbContext.Users
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Email == "admin@factory.local", cancellationToken);

        if (admin is null)
        {
            admin = new User
            {
                Email = "admin@factory.local",
                DisplayName = "Factory Admin",
                PasswordHash = passwordHasher.Hash("Admin123!")
            };
            dbContext.Users.Add(admin);
        }

        if (admin.UserRoles.All(x => x.RoleId != adminRole.Id))
        {
            admin.UserRoles.Add(new UserRole { User = admin, Role = adminRole });
        }

        SeedModules(dbContext);
        SeedWaterTreatmentTags(dbContext);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void SeedModules(AppDbContext dbContext)
    {
        var modules = new[]
        {
            ("dashboard", "Dashboard", "/dashboard", "home", 1, "[\"dashboard.view\"]"),
            ("assets", "Assets", "/assets", "gauge", 2, "[\"assets.view\"]"),
            ("commissioning", "Commissioning", "/commissioning", "clipboard-check", 3, "[\"commissioning.view\"]"),
            ("maintenance", "Maintenance", "/maintenance", "wrench", 4, "[\"maintenance.view\"]"),
            ("warehouse", "Warehouse", "/warehouse", "package", 5, "[\"warehouse.view\"]"),
            ("documents", "Documents", "/documents", "file-text", 6, "[\"documents.view\"]"),
            ("scada", "SCADA", "/scada", "activity", 7, "[\"scada.view\"]"),
            ("reports", "Reports", "/reports", "bar-chart", 8, "[\"reports.view\"]"),
            ("settings", "Settings", "/settings", "settings", 9, "[\"settings.view\"]")
        };

        foreach (var module in modules)
        {
            if (dbContext.Modules.Any(x => x.Key == module.Item1))
            {
                continue;
            }

            dbContext.Modules.Add(new Module
            {
                Key = module.Item1,
                Name = module.Item2,
                Route = module.Item3,
                Icon = module.Item4,
                Order = module.Item5,
                PermissionsJson = module.Item6
            });
        }
    }

    private static void SeedWaterTreatmentTags(AppDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var tags = new[]
        {
            CreateTag("WT_RAW_TANK_1_LEVEL", "Raw Water Tank 1 Level", "Raw Water Tank 1", ProcessTagCategory.Tank, ProcessTagDataType.Number, "%", "72.4", now),
            CreateTag("WT_RAW_TANK_2_LEVEL", "Raw Water Tank 2 Level", "Raw Water Tank 2", ProcessTagCategory.Tank, ProcessTagDataType.Number, "%", "64.8", now),
            CreateTag("WT_RO_TANK_LEVEL", "RO Water Tank Level", "RO Water Tank", ProcessTagCategory.Tank, ProcessTagDataType.Number, "%", "81.2", now),
            CreateTag("WT_SOFT_TANK_LEVEL", "Soft Water Tank Level", "Soft Water Tank", ProcessTagCategory.Tank, ProcessTagDataType.Number, "%", "58.6", now),
            CreateTag("WT_FEED_PUMP_1_STATUS", "Feed Pump 1 Status", "Feed Pump 1", ProcessTagCategory.Pump, ProcessTagDataType.Status, "", "Running", now),
            CreateTag("WT_FEED_PUMP_2_STATUS", "Feed Pump 2 Status", "Feed Pump 2", ProcessTagCategory.Pump, ProcessTagDataType.Status, "", "Standby", now),
            CreateTag("WT_RO_PRESSURE", "RO Pressure", "Reverse Osmosis Skid", ProcessTagCategory.Pressure, ProcessTagDataType.Number, "bar", "9.7", now),
            CreateTag("WT_RO_CONDUCTIVITY", "RO Conductivity", "Reverse Osmosis Skid", ProcessTagCategory.Conductivity, ProcessTagDataType.Number, "uS/cm", "7.8", now),
            CreateTag("WT_RO_FLOW", "RO Flow", "Reverse Osmosis Skid", ProcessTagCategory.Flow, ProcessTagDataType.Number, "m3/h", "12.4", now),
            CreateTag("WT_SOFT_WATER_FLOW", "Soft Water Flow", "Soft Water Header", ProcessTagCategory.Flow, ProcessTagDataType.Number, "m3/h", "18.1", now),
            CreateTag("WT_ACTIVE_ALARMS", "Water Treatment Active Alarms", "Water Treatment", ProcessTagCategory.Alarm, ProcessTagDataType.Number, "alarms", "1", now)
        };

        foreach (var tag in tags)
        {
            var existing = dbContext.ProcessTags.FirstOrDefault(x => x.TagCode == tag.TagCode);
            if (existing is null)
            {
                dbContext.ProcessTags.Add(tag);
                continue;
            }

            existing.Name = tag.Name;
            existing.Area = tag.Area;
            existing.EquipmentName = tag.EquipmentName;
            existing.Category = tag.Category;
            existing.DataType = tag.DataType;
            existing.Unit = tag.Unit;
            existing.Value = tag.Value;
            existing.Quality = tag.Quality;
            existing.Source = tag.Source;
            existing.RefreshInterval = tag.RefreshInterval;
            existing.LastUpdated = now;
            existing.IsActive = true;
        }
    }

    private static ProcessTag CreateTag(
        string tagCode,
        string name,
        string equipmentName,
        ProcessTagCategory category,
        ProcessTagDataType dataType,
        string unit,
        string value,
        DateTime lastUpdated)
    {
        return new ProcessTag
        {
            TagCode = tagCode,
            Name = name,
            Area = "WaterTreatment",
            EquipmentName = equipmentName,
            Category = category,
            DataType = dataType,
            Unit = unit,
            Value = value,
            Quality = ProcessTagQuality.Good,
            Source = ProcessTagSource.Demo,
            PLCAddress = $"DEMO.{tagCode}",
            RefreshInterval = 5,
            LastUpdated = lastUpdated,
            IsActive = true
        };
    }
}
