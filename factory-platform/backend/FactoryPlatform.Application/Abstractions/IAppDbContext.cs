using FactoryPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryPlatform.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Module> Modules { get; }
    DbSet<DashboardLayout> DashboardLayouts { get; }
    DbSet<UserSettings> UserSettings { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<StoredFile> Files { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
