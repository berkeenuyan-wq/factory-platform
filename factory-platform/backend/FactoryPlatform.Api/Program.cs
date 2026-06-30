using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryPlatform.Application.Abstractions;
using FactoryPlatform.Application.Assets;
using FactoryPlatform.Application.Audit;
using FactoryPlatform.Application.Auth;
using FactoryPlatform.Application.Dashboard;
using FactoryPlatform.Application.Modules;
using FactoryPlatform.Application.Settings;
using FactoryPlatform.Application.Users;
using FactoryPlatform.Domain.Entities;
using FactoryPlatform.Domain.Enums;
using FactoryPlatform.Infrastructure;
using FactoryPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanEditDashboard", policy => policy.RequireClaim("permission", "dashboard.edit"));
    options.AddPolicy("CanEditAssets", policy => policy.RequireClaim("permission", "assets.edit"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Factory Platform API", Version = "v0.1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DatabaseSeeder.SeedAsync(dbContext, passwordHasher);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");

api.MapPost("/auth/login", async (
    LoginRequest request,
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    CancellationToken cancellationToken) =>
{
    var user = await dbContext.Users
        .Include(x => x.UserRoles)
        .ThenInclude(x => x.Role)
        .ThenInclude(x => x.RolePermissions)
        .ThenInclude(x => x.Permission)
        .FirstOrDefaultAsync(x => x.Email == request.Email && x.IsActive, cancellationToken);

    if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var roles = user.UserRoles.Select(x => new RoleDto(x.Role.Id, x.Role.Name)).ToArray();
    var permissions = user.UserRoles
        .SelectMany(x => x.Role.RolePermissions)
        .Select(x => x.Permission.Key)
        .Distinct()
        .Order()
        .ToArray();

    var responseUser = new UserMeDto(user.Id, user.Email, user.DisplayName, roles.Select(x => x.Name).ToArray(), permissions);
    var token = jwtTokenService.CreateToken(user, roles, permissions);

    dbContext.AuditLogs.Add(new FactoryPlatform.Domain.Entities.AuditLog
    {
        UserId = user.Id,
        Action = "auth.login",
        EntityName = "User",
        EntityId = user.Id.ToString()
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new LoginResponse(token, responseUser));
});

api.MapGet("/users/me", async (ClaimsPrincipal principal, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var user = await dbContext.Users
        .Include(x => x.UserRoles)
        .ThenInclude(x => x.Role)
        .ThenInclude(x => x.RolePermissions)
        .ThenInclude(x => x.Permission)
        .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    if (user is null)
    {
        return Results.NotFound();
    }

    var roles = user.UserRoles.Select(x => x.Role.Name).Order().ToArray();
    var permissions = user.UserRoles.SelectMany(x => x.Role.RolePermissions).Select(x => x.Permission.Key).Distinct().Order().ToArray();
    return Results.Ok(new UserMeDto(user.Id, user.Email, user.DisplayName, roles, permissions));
}).RequireAuthorization();

api.MapGet("/modules", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var records = await dbContext.Modules
        .Where(x => x.IsEnabled)
        .OrderBy(x => x.Order)
        .ToListAsync(cancellationToken);

    var modules = records
        .Select(x => new ModuleDto(
            x.Key,
            x.Name,
            x.Route,
            x.Icon,
            JsonSerializer.Deserialize<string[]>(x.PermissionsJson, JsonSerializerOptions.Default) ?? Array.Empty<string>(),
            x.Order))
        .ToList();

    return Results.Ok(modules);
}).RequireAuthorization();

api.MapGet("/assets", async (
    string? search,
    AssetCategory? category,
    AssetStatus? status,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.Assets.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(x =>
            x.Code.ToLower().Contains(term) ||
            x.Name.ToLower().Contains(term) ||
            x.Area.ToLower().Contains(term) ||
            x.Manufacturer.ToLower().Contains(term) ||
            x.Model.ToLower().Contains(term) ||
            x.SerialNumber.ToLower().Contains(term));
    }

    if (category is not null)
    {
        query = query.Where(x => x.Category == category);
    }

    if (status is not null)
    {
        query = query.Where(x => x.Status == status);
    }

    var assets = await query
        .OrderBy(x => x.Code)
        .Select(x => AssetHelpers.ToDto(x))
        .ToListAsync(cancellationToken);

    return Results.Ok(assets);
})
.RequireAuthorization()
.WithTags("Assets")
.WithSummary("List assets with optional search, category, and status filters.");

api.MapGet("/assets/{id:guid}", async (Guid id, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var asset = await dbContext.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    return asset is null ? Results.NotFound() : Results.Ok(AssetHelpers.ToDto(asset));
})
.RequireAuthorization()
.WithTags("Assets")
.WithSummary("Get one asset by id.");

api.MapPost("/assets", async (
    ClaimsPrincipal principal,
    UpsertAssetRequest request,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var validation = AssetHelpers.Validate(request);
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    var exists = await dbContext.Assets.AnyAsync(x => x.Code == request.Code.Trim(), cancellationToken);
    if (exists)
    {
        return Results.Conflict($"Asset code '{request.Code.Trim()}' already exists.");
    }

    var asset = new Asset();
    AssetHelpers.Apply(asset, request);
    dbContext.Assets.Add(asset);
    AssetHelpers.AddAudit(dbContext, principal.GetUserId(), "asset.created", asset.Id);

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/assets/{asset.Id}", AssetHelpers.ToDto(asset));
})
.RequireAuthorization("CanEditAssets")
.WithTags("Assets")
.WithSummary("Create an asset.");

api.MapPut("/assets/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    UpsertAssetRequest request,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var validation = AssetHelpers.Validate(request);
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    var asset = await dbContext.Assets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (asset is null)
    {
        return Results.NotFound();
    }

    var code = request.Code.Trim();
    var duplicate = await dbContext.Assets.AnyAsync(x => x.Id != id && x.Code == code, cancellationToken);
    if (duplicate)
    {
        return Results.Conflict($"Asset code '{code}' already exists.");
    }

    AssetHelpers.Apply(asset, request);
    AssetHelpers.AddAudit(dbContext, principal.GetUserId(), "asset.updated", asset.Id);

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(AssetHelpers.ToDto(asset));
})
.RequireAuthorization("CanEditAssets")
.WithTags("Assets")
.WithSummary("Update an asset.");

api.MapDelete("/assets/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var asset = await dbContext.Assets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (asset is null)
    {
        return Results.NotFound();
    }

    dbContext.Assets.Remove(asset);
    AssetHelpers.AddAudit(dbContext, principal.GetUserId(), "asset.deleted", asset.Id);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.NoContent();
})
.RequireAuthorization("CanEditAssets")
.WithTags("Assets")
.WithSummary("Delete an asset.");

api.MapGet("/dashboard/layout", async (ClaimsPrincipal principal, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var layout = await DashboardHelpers.GetOrCreateDefaultDashboardAsync(userId, dbContext, cancellationToken);

    return Results.Ok(DashboardHelpers.ToDashboardDto(layout));
}).RequireAuthorization();

api.MapGet("/dashboard/layouts", async (ClaimsPrincipal principal, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    await DashboardHelpers.GetOrCreateDefaultDashboardAsync(userId, dbContext, cancellationToken);

    var layouts = await dbContext.DashboardLayouts
        .Where(x => x.UserId == userId)
        .OrderBy(x => x.Name == "Default" ? 0 : 1)
        .ThenBy(x => x.Name)
        .Select(x => new DashboardLayoutDto(x.Id, x.UserId, x.Name, x.LayoutJson, x.UpdatedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(layouts);
}).RequireAuthorization();

api.MapPost("/dashboard/layouts", async (
    ClaimsPrincipal principal,
    CreateDashboardLayoutRequest request,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    JsonDocument.Parse(request.LayoutJson);
    var userId = principal.GetUserId();
    var name = DashboardHelpers.NormalizeDashboardName(request.Name);

    var existing = await dbContext.DashboardLayouts.AnyAsync(x => x.UserId == userId && x.Name == name, cancellationToken);
    if (existing)
    {
        return Results.Conflict($"Dashboard '{name}' already exists.");
    }

    var layout = new FactoryPlatform.Domain.Entities.DashboardLayout
    {
        UserId = userId,
        Name = name,
        LayoutJson = request.LayoutJson,
        UpdatedAtUtc = DateTime.UtcNow
    };

    dbContext.DashboardLayouts.Add(layout);
    dbContext.AuditLogs.Add(new FactoryPlatform.Domain.Entities.AuditLog
    {
        UserId = userId,
        Action = "dashboard.created",
        EntityName = "DashboardLayout",
        EntityId = layout.Id.ToString()
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/dashboard/layouts/{layout.Id}", DashboardHelpers.ToDashboardDto(layout));
}).RequireAuthorization("CanEditDashboard");

api.MapGet("/dashboard/layouts/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var layout = await dbContext.DashboardLayouts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    if (layout is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(DashboardHelpers.ToDashboardDto(layout));
}).RequireAuthorization();

api.MapPut("/dashboard/layout", async (
    ClaimsPrincipal principal,
    UpdateDashboardLayoutRequest request,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    JsonDocument.Parse(request.LayoutJson);
    var userId = principal.GetUserId();
    var layout = await DashboardHelpers.GetOrCreateDefaultDashboardAsync(userId, dbContext, cancellationToken);

    layout.LayoutJson = request.LayoutJson;
    layout.UpdatedAtUtc = DateTime.UtcNow;
    dbContext.AuditLogs.Add(new FactoryPlatform.Domain.Entities.AuditLog
    {
        UserId = userId,
        Action = "dashboard.layout.updated",
        EntityName = "DashboardLayout",
        EntityId = layout.Id.ToString()
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(DashboardHelpers.ToDashboardDto(layout));
}).RequireAuthorization("CanEditDashboard");

api.MapPut("/dashboard/layouts/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    UpdateDashboardLayoutRequest request,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    JsonDocument.Parse(request.LayoutJson);
    var userId = principal.GetUserId();
    var layout = await dbContext.DashboardLayouts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    if (layout is null)
    {
        return Results.NotFound();
    }

    layout.Name = DashboardHelpers.NormalizeDashboardName(request.Name);
    layout.LayoutJson = request.LayoutJson;
    layout.UpdatedAtUtc = DateTime.UtcNow;

    dbContext.AuditLogs.Add(new FactoryPlatform.Domain.Entities.AuditLog
    {
        UserId = userId,
        Action = "dashboard.layout.updated",
        EntityName = "DashboardLayout",
        EntityId = layout.Id.ToString()
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(DashboardHelpers.ToDashboardDto(layout));
}).RequireAuthorization("CanEditDashboard");

api.MapGet("/settings", async (ClaimsPrincipal principal, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var settings = await dbContext.UserSettings.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    if (settings is null)
    {
        settings = new FactoryPlatform.Domain.Entities.UserSettings
        {
            UserId = userId,
            SettingsJson = "{\"theme\":\"dark\",\"density\":\"comfortable\"}"
        };
        dbContext.UserSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new UserSettingsDto(settings.Id, settings.UserId, settings.SettingsJson));
}).RequireAuthorization();

api.MapPut("/settings", async (
    ClaimsPrincipal principal,
    UpdateUserSettingsRequest request,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    JsonDocument.Parse(request.SettingsJson);
    var userId = principal.GetUserId();
    var settings = await dbContext.UserSettings.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    if (settings is null)
    {
        settings = new FactoryPlatform.Domain.Entities.UserSettings { UserId = userId };
        dbContext.UserSettings.Add(settings);
    }

    settings.SettingsJson = request.SettingsJson;
    settings.UpdatedAtUtc = DateTime.UtcNow;
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new UserSettingsDto(settings.Id, settings.UserId, settings.SettingsJson));
}).RequireAuthorization();

api.MapGet("/audit-logs", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var logs = await dbContext.AuditLogs
        .OrderByDescending(x => x.CreatedAtUtc)
        .Take(100)
        .Select(x => new AuditLogDto(x.Id, x.Action, x.EntityName, x.EntityId, x.UserId, x.CreatedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(logs);
}).RequireAuthorization();

app.Run();

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}

internal static class DashboardHelpers
{
    public static async Task<FactoryPlatform.Domain.Entities.DashboardLayout> GetOrCreateDefaultDashboardAsync(
        Guid userId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var layout = await dbContext.DashboardLayouts.FirstOrDefaultAsync(x => x.UserId == userId && x.Name == "Default", cancellationToken);
        if (layout is not null)
        {
            return layout;
        }

        layout = new FactoryPlatform.Domain.Entities.DashboardLayout
        {
            UserId = userId,
            Name = "Default",
            LayoutJson = "[{\"id\":\"kpi-card-0\",\"type\":\"kpi-card\",\"size\":\"small\"},{\"id\":\"chart-placeholder-1\",\"type\":\"chart-placeholder\",\"size\":\"medium\"},{\"id\":\"table-placeholder-2\",\"type\":\"table-placeholder\",\"size\":\"wide\"},{\"id\":\"alarm-placeholder-3\",\"type\":\"alarm-placeholder\",\"size\":\"small\"}]"
        };
        dbContext.DashboardLayouts.Add(layout);
        await dbContext.SaveChangesAsync(cancellationToken);

        return layout;
    }

    public static DashboardLayoutDto ToDashboardDto(FactoryPlatform.Domain.Entities.DashboardLayout layout)
    {
        return new DashboardLayoutDto(layout.Id, layout.UserId, layout.Name, layout.LayoutJson, layout.UpdatedAtUtc);
    }

    public static string NormalizeDashboardName(string name)
    {
        var normalized = name.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "Untitled Dashboard" : normalized;
    }
}

internal static class AssetHelpers
{
    public static AssetDto ToDto(Asset asset)
    {
        return new AssetDto(
            asset.Id,
            asset.Code,
            asset.Name,
            asset.Category,
            asset.Area,
            asset.Manufacturer,
            asset.Model,
            asset.SerialNumber,
            asset.Status,
            asset.Notes,
            asset.CreatedAtUtc,
            asset.UpdatedAtUtc);
    }

    public static string? Validate(UpsertAssetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Asset code is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Asset name is required.";
        }

        return null;
    }

    public static void Apply(Asset asset, UpsertAssetRequest request)
    {
        asset.Code = request.Code.Trim();
        asset.Name = request.Name.Trim();
        asset.Category = request.Category;
        asset.Area = request.Area.Trim();
        asset.Manufacturer = request.Manufacturer.Trim();
        asset.Model = request.Model.Trim();
        asset.SerialNumber = request.SerialNumber.Trim();
        asset.Status = request.Status;
        asset.Notes = request.Notes.Trim();
        asset.UpdatedAtUtc = DateTime.UtcNow;
    }

    public static void AddAudit(AppDbContext dbContext, Guid userId, string action, Guid assetId)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = "Asset",
            EntityId = assetId.ToString()
        });
    }
}
