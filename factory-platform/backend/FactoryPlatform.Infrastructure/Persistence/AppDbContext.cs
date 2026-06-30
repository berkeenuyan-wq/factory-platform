using FactoryPlatform.Application.Abstractions;
using FactoryPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryPlatform.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentAllowedRole> DocumentAllowedRoles => Set<DocumentAllowedRole>();
    public DbSet<ProcessTag> ProcessTags => Set<ProcessTag>();
    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StoredFile> Files => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.DisplayName).HasMaxLength(160);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80);
        });

        modelBuilder.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        modelBuilder.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(120);
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Route).HasMaxLength(160);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(180);
            entity.Property(x => x.Area).HasMaxLength(120);
            entity.Property(x => x.Manufacturer).HasMaxLength(160);
            entity.Property(x => x.Model).HasMaxLength(160);
            entity.Property(x => x.SerialNumber).HasMaxLength(160);
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(x => x.Title);
            entity.HasIndex(x => x.DocumentType);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.AssetId);
            entity.Property(x => x.Title).HasMaxLength(220);
            entity.Property(x => x.Revision).HasMaxLength(60);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.FileExtension).HasMaxLength(40);
            entity.Property(x => x.StoragePath).HasMaxLength(600);
            entity.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(60);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentAllowedRole>(entity =>
        {
            entity.HasKey(x => new { x.DocumentId, x.RoleId });
            entity.HasOne(x => x.Document).WithMany(x => x.AllowedRoles).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessTag>(entity =>
        {
            entity.HasIndex(x => x.TagCode).IsUnique();
            entity.HasIndex(x => x.Area);
            entity.HasIndex(x => x.Category);
            entity.Property(x => x.TagCode).HasMaxLength(120);
            entity.Property(x => x.Name).HasMaxLength(180);
            entity.Property(x => x.Area).HasMaxLength(120);
            entity.Property(x => x.EquipmentName).HasMaxLength(180);
            entity.Property(x => x.Unit).HasMaxLength(40);
            entity.Property(x => x.Value).HasMaxLength(220);
            entity.Property(x => x.PLCAddress).HasMaxLength(220);
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.DataType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Quality).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<DashboardLayout>()
            .HasIndex(x => new { x.UserId, x.Name })
            .IsUnique();

        modelBuilder.Entity<UserSettings>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<StoredFile>().ToTable("Files");
    }
}
