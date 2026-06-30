using FactoryPlatform.Application.Abstractions;
using FactoryPlatform.Application.Common;
using FactoryPlatform.Domain.Entities;
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

        var adminRole = await dbContext.Roles
            .Include(x => x.RolePermissions)
            .FirstOrDefaultAsync(x => x.Name == "Admin", cancellationToken);

        if (adminRole is null)
        {
            adminRole = new Role { Name = "Admin", Description = "Platform administrator" };
            dbContext.Roles.Add(adminRole);
        }

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
}
