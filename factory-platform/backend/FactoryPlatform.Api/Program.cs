using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactoryPlatform.Application.Abstractions;
using FactoryPlatform.Application.Assets;
using FactoryPlatform.Application.Audit;
using FactoryPlatform.Application.Auth;
using FactoryPlatform.Application.Dashboard;
using FactoryPlatform.Application.Documents;
using FactoryPlatform.Application.Modules;
using FactoryPlatform.Application.ProcessTags;
using FactoryPlatform.Application.Settings;
using FactoryPlatform.Application.Users;
using FactoryPlatform.Domain.Entities;
using FactoryPlatform.Domain.Enums;
using FactoryPlatform.Infrastructure;
using FactoryPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
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
    options.AddPolicy("CanUploadDocuments", policy => policy.RequireClaim("permission", "documents.upload"));
    options.AddPolicy("CanEditDocuments", policy => policy.RequireClaim("permission", "documents.edit"));
    options.AddPolicy("CanDeleteDocuments", policy => policy.RequireClaim("permission", "documents.delete"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FactoryOS API", Version = "v0.4" });
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

api.MapGet("/roles/options", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var roles = await dbContext.Roles
        .AsNoTracking()
        .OrderBy(x => x.Name)
        .Select(x => new RoleOptionDto(x.Id, x.Name))
        .ToArrayAsync(cancellationToken);

    return Results.Ok(roles);
})
.RequireAuthorization()
.WithTags("Roles")
.WithSummary("List roles for permission selectors.");

api.MapGet("/process-tags", async (
    string? search,
    string? area,
    ProcessTagCategory? category,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.ProcessTags.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(x =>
            x.TagCode.ToLower().Contains(term) ||
            x.Name.ToLower().Contains(term) ||
            x.Area.ToLower().Contains(term) ||
            x.EquipmentName.ToLower().Contains(term));
    }

    if (!string.IsNullOrWhiteSpace(area))
    {
        var normalizedArea = area.Trim().ToLower();
        query = query.Where(x => x.Area.ToLower() == normalizedArea);
    }

    if (category is not null)
    {
        query = query.Where(x => x.Category == category);
    }

    var tags = await query
        .OrderBy(x => x.Area)
        .ThenBy(x => x.TagCode)
        .Select(x => ProcessTagHelpers.ToDto(x))
        .ToListAsync(cancellationToken);

    return Results.Ok(tags);
})
.RequireAuthorization()
.WithTags("Process Tags")
.WithSummary("List process tags with optional search, area, and category filters.");

api.MapGet("/process-tags/current", async (
    string codes,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var requestedCodes = ProcessTagHelpers.ParseCodes(codes);
    var tags = await dbContext.ProcessTags
        .AsNoTracking()
        .Where(x => requestedCodes.Contains(x.TagCode))
        .OrderBy(x => x.TagCode)
        .Select(x => ProcessTagHelpers.ToDto(x))
        .ToListAsync(cancellationToken);

    return Results.Ok(tags);
})
.RequireAuthorization()
.WithTags("Process Tags")
.WithSummary("Return current values for a comma-separated set of tag codes.");

api.MapGet("/process-tags/by-area/{area}", async (
    string area,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var normalizedArea = area.Trim().ToLower();
    var tags = await dbContext.ProcessTags
        .AsNoTracking()
        .Where(x => x.Area.ToLower() == normalizedArea)
        .OrderBy(x => x.TagCode)
        .Select(x => ProcessTagHelpers.ToDto(x))
        .ToListAsync(cancellationToken);

    return Results.Ok(tags);
})
.RequireAuthorization()
.WithTags("Process Tags")
.WithSummary("Return all process tags for an area.");

api.MapGet("/process-tags/{tagCode}", async (
    string tagCode,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var normalizedCode = tagCode.Trim().ToUpperInvariant();
    var tag = await dbContext.ProcessTags
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.TagCode == normalizedCode, cancellationToken);

    return tag is null ? Results.NotFound() : Results.Ok(ProcessTagHelpers.ToDto(tag));
})
.RequireAuthorization()
.WithTags("Process Tags")
.WithSummary("Return one process tag by tag code.");

api.MapGet("/widgets/water-treatment", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var codes = ProcessTagHelpers.WaterTreatmentTagCodes.Values.ToArray();
    var tags = await dbContext.ProcessTags
        .AsNoTracking()
        .Where(x => codes.Contains(x.TagCode))
        .ToDictionaryAsync(x => x.TagCode, x => x, cancellationToken);

    return Results.Ok(ProcessTagHelpers.ToWaterTreatmentWidget(tags));
})
.RequireAuthorization()
.WithTags("Widgets")
.WithSummary("Return the Water Treatment Overview widget data from process tags.");

api.MapGet("/documents", async (
    ClaimsPrincipal principal,
    string? search,
    DocumentType? documentType,
    DocumentStatus? status,
    Guid? assetId,
    int? page,
    int? pageSize,
    string? sortBy,
    string? sortDirection,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var access = await DocumentHelpers.GetAccessContextAsync(principal, dbContext, cancellationToken);
    var query = dbContext.Documents
        .AsNoTracking()
        .Include(x => x.Asset)
        .Include(x => x.UploadedByUser)
        .Include(x => x.AllowedRoles)
        .ThenInclude(x => x.Role)
        .AsQueryable();

    query = DocumentHelpers.ApplyVisibility(query, access);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(x =>
            x.Title.ToLower().Contains(term) ||
            x.Description.ToLower().Contains(term) ||
            x.Revision.ToLower().Contains(term) ||
            x.OriginalFileName.ToLower().Contains(term));
    }

    if (documentType is not null)
    {
        query = query.Where(x => x.DocumentType == documentType);
    }

    if (status is not null)
    {
        query = query.Where(x => x.Status == status);
    }

    if (assetId is not null)
    {
        query = query.Where(x => x.AssetId == assetId);
    }

    var totalCount = await query.CountAsync(cancellationToken);
    var currentPage = Math.Max(1, page ?? 1);
    var currentPageSize = Math.Clamp(pageSize ?? 20, 5, 100);
    var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
    query = DocumentHelpers.ApplySort(query, sortBy, descending);

    var documents = await query
        .Skip((currentPage - 1) * currentPageSize)
        .Take(currentPageSize)
        .ToListAsync(cancellationToken);

    return Results.Ok(new DocumentListResponse(
        documents.Select(DocumentHelpers.ToDto).ToArray(),
        totalCount,
        currentPage,
        currentPageSize));
})
.RequireAuthorization()
.WithTags("Documents")
.WithSummary("List accessible documents with search, filters, sorting, and pagination.");

api.MapGet("/documents/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var document = await DocumentHelpers.GetDocumentAsync(id, dbContext, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    var access = await DocumentHelpers.GetAccessContextAsync(principal, dbContext, cancellationToken);
    if (!DocumentHelpers.CanAccess(document, access))
    {
        DocumentHelpers.AddAudit(dbContext, access.UserId, "document.permission_denied", document.Id, "view");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Forbid();
    }

    DocumentHelpers.AddAudit(dbContext, access.UserId, "document.viewed", document.Id);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(DocumentHelpers.ToDto(document));
})
.RequireAuthorization()
.WithTags("Documents")
.WithSummary("Get document metadata when visible to the current user.");

api.MapPost("/documents/upload", async (
    HttpRequest request,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    IFileStorageService fileStorage,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Multipart form data is required.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("A document file is required.");
    }

    var uploadRequest = DocumentHelpers.ParseUploadRequest(form);
    var validation = DocumentHelpers.Validate(uploadRequest);
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    var userId = principal.GetUserId();
    var assetValidation = await DocumentHelpers.ValidateAssetAsync(uploadRequest.AssetId, dbContext, cancellationToken);
    if (assetValidation is not null)
    {
        return Results.BadRequest(assetValidation);
    }
    var originalFileName = Path.GetFileName(file.FileName);
    await using var stream = file.OpenReadStream();
    var storagePath = await fileStorage.SaveAsync(stream, originalFileName, cancellationToken);

    var document = new Document
    {
        Title = uploadRequest.Title.Trim(),
        Description = uploadRequest.Description.Trim(),
        DocumentType = uploadRequest.DocumentType,
        Revision = uploadRequest.Revision.Trim(),
        AssetId = uploadRequest.AssetId,
        FileName = Path.GetFileName(storagePath),
        OriginalFileName = originalFileName,
        FileExtension = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant(),
        FileSize = file.Length,
        StoragePath = storagePath,
        UploadedBy = userId,
        UploadedAt = DateTime.UtcNow,
        LastModified = DateTime.UtcNow,
        Status = uploadRequest.Status,
        Visibility = uploadRequest.Visibility
    };

    DocumentHelpers.SetAllowedRoles(document, uploadRequest.AllowedRoleIds);
    dbContext.Documents.Add(document);
    DocumentHelpers.AddAudit(dbContext, userId, "document.uploaded", document.Id);
    await dbContext.SaveChangesAsync(cancellationToken);

    var created = await DocumentHelpers.GetDocumentAsync(document.Id, dbContext, cancellationToken);
    return Results.Created($"/api/documents/{document.Id}", DocumentHelpers.ToDto(created!));
})
.RequireAuthorization("CanUploadDocuments")
.DisableAntiforgery()
.WithTags("Documents")
.WithSummary("Upload a document file and metadata.");

api.MapPut("/documents/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    UpdateDocumentRequest request,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var validation = DocumentHelpers.Validate(request);
    if (validation is not null)
    {
        return Results.BadRequest(validation);
    }

    var document = await dbContext.Documents
        .Include(x => x.AllowedRoles)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    var access = await DocumentHelpers.GetAccessContextAsync(principal, dbContext, cancellationToken);
    if (!DocumentHelpers.CanAccess(document, access))
    {
        DocumentHelpers.AddAudit(dbContext, access.UserId, "document.permission_denied", document.Id, "update");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Forbid();
    }

    var assetValidation = await DocumentHelpers.ValidateAssetAsync(request.AssetId, dbContext, cancellationToken);
    if (assetValidation is not null)
    {
        return Results.BadRequest(assetValidation);
    }
    document.Title = request.Title.Trim();
    document.Description = request.Description.Trim();
    document.DocumentType = request.DocumentType;
    document.Revision = request.Revision.Trim();
    document.AssetId = request.AssetId;
    document.Status = request.Status;
    document.Visibility = request.Visibility;
    document.LastModified = DateTime.UtcNow;
    DocumentHelpers.ReplaceAllowedRoles(document, request.AllowedRoleIds);
    DocumentHelpers.AddAudit(dbContext, access.UserId, "document.updated", document.Id);
    await dbContext.SaveChangesAsync(cancellationToken);

    var updated = await DocumentHelpers.GetDocumentAsync(id, dbContext, cancellationToken);
    return Results.Ok(DocumentHelpers.ToDto(updated!));
})
.RequireAuthorization("CanEditDocuments")
.WithTags("Documents")
.WithSummary("Edit document metadata and visibility.");

api.MapDelete("/documents/{id:guid}", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    IFileStorageService fileStorage,
    CancellationToken cancellationToken) =>
{
    var document = await dbContext.Documents
        .Include(x => x.AllowedRoles)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    var access = await DocumentHelpers.GetAccessContextAsync(principal, dbContext, cancellationToken);
    if (!DocumentHelpers.CanAccess(document, access))
    {
        DocumentHelpers.AddAudit(dbContext, access.UserId, "document.permission_denied", document.Id, "delete");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Forbid();
    }

    var storagePath = document.StoragePath;
    dbContext.Documents.Remove(document);
    DocumentHelpers.AddAudit(dbContext, access.UserId, "document.deleted", document.Id);
    await dbContext.SaveChangesAsync(cancellationToken);
    await fileStorage.DeleteAsync(storagePath, cancellationToken);

    return Results.NoContent();
})
.RequireAuthorization("CanDeleteDocuments")
.WithTags("Documents")
.WithSummary("Delete a document and its stored file.");

api.MapGet("/documents/{id:guid}/download", async (
    Guid id,
    ClaimsPrincipal principal,
    AppDbContext dbContext,
    IFileStorageService fileStorage,
    CancellationToken cancellationToken) =>
{
    var document = await DocumentHelpers.GetDocumentAsync(id, dbContext, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    var access = await DocumentHelpers.GetAccessContextAsync(principal, dbContext, cancellationToken);
    if (!DocumentHelpers.CanAccess(document, access))
    {
        DocumentHelpers.AddAudit(dbContext, access.UserId, "document.permission_denied", document.Id, "download");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Forbid();
    }

    DocumentHelpers.AddAudit(dbContext, access.UserId, "document.downloaded", document.Id);
    await dbContext.SaveChangesAsync(cancellationToken);
    var fileStream = await fileStorage.OpenReadAsync(document.StoragePath, cancellationToken);
    return Results.File(fileStream, DocumentHelpers.GetContentType(document.FileExtension), document.OriginalFileName);
})
.RequireAuthorization()
.WithTags("Documents")
.WithSummary("Download a visible document file.");

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

api.MapGet("/audit-logs", async (
    string? search,
    string? action,
    string? entityName,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.AuditLogs.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(action))
    {
        query = query.Where(x => x.Action == action.Trim());
    }

    if (!string.IsNullOrWhiteSpace(entityName))
    {
        query = query.Where(x => x.EntityName == entityName.Trim());
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(x =>
            x.Action.ToLower().Contains(term) ||
            x.EntityName.ToLower().Contains(term) ||
            (x.EntityId != null && x.EntityId.ToLower().Contains(term)) ||
            x.MetadataJson.ToLower().Contains(term));
    }

    var logs = await query
        .OrderByDescending(x => x.CreatedAtUtc)
        .Take(100)
        .Select(x => new AuditLogDto(x.Id, x.Action, x.EntityName, x.EntityId, x.UserId, x.CreatedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(logs);
}).RequireAuthorization()
.WithTags("Audit")
.WithSummary("List recent audit logs with optional search, action, and entity filters.");

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

internal static class ProcessTagHelpers
{
    public static readonly Dictionary<string, string> WaterTreatmentTagCodes = new()
    {
        ["rawTank1Level"] = "WT_RAW_TANK_1_LEVEL",
        ["rawTank2Level"] = "WT_RAW_TANK_2_LEVEL",
        ["roTankLevel"] = "WT_RO_TANK_LEVEL",
        ["softTankLevel"] = "WT_SOFT_TANK_LEVEL",
        ["feedPump1Status"] = "WT_FEED_PUMP_1_STATUS",
        ["feedPump2Status"] = "WT_FEED_PUMP_2_STATUS",
        ["roPressure"] = "WT_RO_PRESSURE",
        ["roConductivity"] = "WT_RO_CONDUCTIVITY",
        ["roFlow"] = "WT_RO_FLOW",
        ["softWaterFlow"] = "WT_SOFT_WATER_FLOW",
        ["activeAlarms"] = "WT_ACTIVE_ALARMS"
    };

    public static string[] ParseCodes(string codes)
    {
        return codes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .Distinct()
            .ToArray();
    }

    public static ProcessTagDto ToDto(ProcessTag tag)
    {
        return new ProcessTagDto(
            tag.Id,
            tag.TagCode,
            tag.Name,
            tag.Area,
            tag.EquipmentId,
            tag.EquipmentName,
            tag.Category,
            tag.DataType,
            tag.Unit,
            tag.Value,
            tag.Quality,
            tag.Source,
            tag.PLCAddress,
            tag.RefreshInterval,
            tag.LastUpdated,
            tag.IsActive);
    }

    public static WaterTreatmentWidgetDto ToWaterTreatmentWidget(IReadOnlyDictionary<string, ProcessTag> tags)
    {
        var allTags = WaterTreatmentTagCodes.Values
            .Select(code => GetTag(tags, code))
            .ToArray();
        var lastUpdated = allTags.Max(x => x.LastUpdated);

        return new WaterTreatmentWidgetDto(
            "WaterTreatment",
            lastUpdated,
            [
                Tank("Raw Water Tank 1", tags, WaterTreatmentTagCodes["rawTank1Level"]),
                Tank("Raw Water Tank 2", tags, WaterTreatmentTagCodes["rawTank2Level"]),
                Tank("RO Water Tank", tags, WaterTreatmentTagCodes["roTankLevel"]),
                Tank("Soft Water Tank", tags, WaterTreatmentTagCodes["softTankLevel"])
            ],
            Metric("Feed Pump 1", tags, WaterTreatmentTagCodes["feedPump1Status"]),
            Metric("Feed Pump 2", tags, WaterTreatmentTagCodes["feedPump2Status"]),
            Metric("RO Pressure", tags, WaterTreatmentTagCodes["roPressure"]),
            Metric("RO Conductivity", tags, WaterTreatmentTagCodes["roConductivity"]),
            Metric("RO Flow", tags, WaterTreatmentTagCodes["roFlow"]),
            Metric("Soft Water Flow", tags, WaterTreatmentTagCodes["softWaterFlow"]),
            Metric("Active Alarms", tags, WaterTreatmentTagCodes["activeAlarms"]));
    }

    private static WaterTreatmentTankDto Tank(string name, IReadOnlyDictionary<string, ProcessTag> tags, string code)
    {
        var tag = GetTag(tags, code);
        var level = decimal.TryParse(tag.Value, out var parsed) ? parsed : 0;
        var status = tag.Quality == ProcessTagQuality.Good && level > 15 ? "Normal" : "Check";
        return new WaterTreatmentTankDto(name, tag.TagCode, level, tag.Unit, status, tag.LastUpdated);
    }

    private static WaterTreatmentMetricDto Metric(string label, IReadOnlyDictionary<string, ProcessTag> tags, string code)
    {
        var tag = GetTag(tags, code);
        return new WaterTreatmentMetricDto(label, tag.TagCode, tag.Value, tag.Unit, tag.Quality, tag.LastUpdated);
    }

    private static ProcessTag GetTag(IReadOnlyDictionary<string, ProcessTag> tags, string code)
    {
        if (tags.TryGetValue(code, out var tag))
        {
            return tag;
        }

        return new ProcessTag
        {
            TagCode = code,
            Name = code,
            Area = "WaterTreatment",
            Value = "0",
            Unit = string.Empty,
            Quality = ProcessTagQuality.Bad,
            Source = ProcessTagSource.Demo,
            LastUpdated = DateTime.UtcNow,
            IsActive = false
        };
    }
}

internal sealed record DocumentAccessContext(Guid UserId, HashSet<Guid> RoleIds, HashSet<string> RoleNames)
{
    public bool IsAdministrator => RoleNames.Contains("Admin") || RoleNames.Contains("Administrator");
}

internal static class DocumentHelpers
{
    public static async Task<DocumentAccessContext> GetAccessContextAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Role)
            .Select(x => new { x.RoleId, x.Role.Name })
            .ToListAsync(cancellationToken);

        return new DocumentAccessContext(
            userId,
            roles.Select(x => x.RoleId).ToHashSet(),
            roles.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    public static IQueryable<Document> ApplyVisibility(IQueryable<Document> query, DocumentAccessContext access)
    {
        if (access.IsAdministrator)
        {
            return query;
        }

        return query.Where(x =>
            x.Visibility == DocumentVisibility.Public ||
            (x.Visibility == DocumentVisibility.RoleRestricted && x.AllowedRoles.Any(role => access.RoleIds.Contains(role.RoleId))));
    }

    public static bool CanAccess(Document document, DocumentAccessContext access)
    {
        if (access.IsAdministrator || document.Visibility == DocumentVisibility.Public)
        {
            return true;
        }

        return document.Visibility == DocumentVisibility.RoleRestricted &&
            document.AllowedRoles.Any(role => access.RoleIds.Contains(role.RoleId));
    }

    public static async Task<Document?> GetDocumentAsync(Guid id, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Documents
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.UploadedByUser)
            .Include(x => x.AllowedRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public static IQueryable<Document> ApplySort(IQueryable<Document> query, string? sortBy, bool descending)
    {
        return (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("title", false) => query.OrderBy(x => x.Title),
            ("title", true) => query.OrderByDescending(x => x.Title),
            ("type", false) => query.OrderBy(x => x.DocumentType).ThenBy(x => x.Title),
            ("type", true) => query.OrderByDescending(x => x.DocumentType).ThenBy(x => x.Title),
            ("status", false) => query.OrderBy(x => x.Status).ThenBy(x => x.Title),
            ("status", true) => query.OrderByDescending(x => x.Status).ThenBy(x => x.Title),
            ("revision", false) => query.OrderBy(x => x.Revision).ThenBy(x => x.Title),
            ("revision", true) => query.OrderByDescending(x => x.Revision).ThenBy(x => x.Title),
            ("uploadedat", false) => query.OrderBy(x => x.UploadedAt),
            _ => query.OrderByDescending(x => x.UploadedAt)
        };
    }

    public static DocumentDto ToDto(Document document)
    {
        return new DocumentDto(
            document.Id,
            document.Title,
            document.Description,
            document.DocumentType,
            document.Revision,
            document.AssetId,
            document.Asset?.Code,
            document.Asset?.Name,
            document.FileName,
            document.OriginalFileName,
            document.FileExtension,
            document.FileSize,
            document.UploadedBy,
            document.UploadedByUser?.DisplayName,
            document.UploadedAt,
            document.LastModified,
            document.Status,
            document.Visibility,
            document.AllowedRoles
                .OrderBy(x => x.Role.Name)
                .Select(x => new RoleOptionDto(x.RoleId, x.Role.Name))
                .ToArray());
    }

    public static DocumentUploadRequest ParseUploadRequest(IFormCollection form)
    {
        return new DocumentUploadRequest(
            GetFormString(form, "title"),
            GetFormString(form, "description"),
            ParseEnum<DocumentType>(form, "documentType", DocumentType.Other),
            GetFormString(form, "revision"),
            ParseNullableGuid(form, "assetId"),
            ParseEnum<DocumentStatus>(form, "status", DocumentStatus.Draft),
            ParseEnum<DocumentVisibility>(form, "visibility", DocumentVisibility.Public),
            ParseGuidArray(form, "allowedRoleIds"));
    }

    public static string? Validate(DocumentUploadRequest request)
    {
        return Validate(request.Title, request.Revision, request.Visibility, request.AllowedRoleIds);
    }

    public static string? Validate(UpdateDocumentRequest request)
    {
        return Validate(request.Title, request.Revision, request.Visibility, request.AllowedRoleIds);
    }

    public static async Task<string?> ValidateAssetAsync(Guid? assetId, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (assetId is null)
        {
            return null;
        }

        return await dbContext.Assets.AnyAsync(x => x.Id == assetId, cancellationToken)
            ? null
            : "Linked asset was not found.";
    }

    public static void SetAllowedRoles(Document document, Guid[] roleIds)
    {
        if (document.Visibility != DocumentVisibility.RoleRestricted)
        {
            return;
        }

        foreach (var roleId in roleIds.Distinct())
        {
            document.AllowedRoles.Add(new DocumentAllowedRole { DocumentId = document.Id, RoleId = roleId });
        }
    }

    public static void ReplaceAllowedRoles(Document document, Guid[] roleIds)
    {
        document.AllowedRoles.Clear();
        SetAllowedRoles(document, roleIds);
    }

    public static void AddAudit(AppDbContext dbContext, Guid userId, string action, Guid documentId, string? operation = null)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = "Document",
            EntityId = documentId.ToString(),
            MetadataJson = operation is null ? "{}" : JsonSerializer.Serialize(new { operation })
        });
    }

    public static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            "txt" => "text/plain",
            "csv" => "text/csv",
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "mp4" => "video/mp4",
            "doc" => "application/msword",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xls" => "application/vnd.ms-excel",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }

    private static string? Validate(string title, string revision, DocumentVisibility visibility, Guid[] allowedRoleIds)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Document title is required.";
        }

        if (string.IsNullOrWhiteSpace(revision))
        {
            return "Document revision is required.";
        }

        if (visibility == DocumentVisibility.RoleRestricted && allowedRoleIds.Length == 0)
        {
            return "Role restricted documents require at least one allowed role.";
        }

        return null;
    }

    private static string GetFormString(IFormCollection form, string key)
    {
        return form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }

    private static T ParseEnum<T>(IFormCollection form, string key, T fallback) where T : struct
    {
        var raw = GetFormString(form, key);
        return Enum.TryParse<T>(raw, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static Guid? ParseNullableGuid(IFormCollection form, string key)
    {
        var raw = GetFormString(form, key);
        return Guid.TryParse(raw, out var value) ? value : null;
    }

    private static Guid[] ParseGuidArray(IFormCollection form, string key)
    {
        var raw = GetFormString(form, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Guid[]>(raw);
            if (parsed is not null)
            {
                return parsed;
            }
        }
        catch (JsonException)
        {
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToArray();
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
