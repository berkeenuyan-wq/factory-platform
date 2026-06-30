using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FactoryPlatform.Application.Abstractions;
using FactoryPlatform.Application.Audit;
using FactoryPlatform.Application.Auth;
using FactoryPlatform.Application.Dashboard;
using FactoryPlatform.Application.Modules;
using FactoryPlatform.Application.Settings;
using FactoryPlatform.Application.Users;
using FactoryPlatform.Infrastructure;
using FactoryPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
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
