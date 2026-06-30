using System;
using FactoryPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260630212000_AddDocuments")]
public partial class AddDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Documents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                DocumentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Revision = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                FileExtension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                StoragePath = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Documents", x => x.Id);
                table.ForeignKey(
                    name: "FK_Documents_Assets_AssetId",
                    column: x => x.AssetId,
                    principalTable: "Assets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Documents_Users_UploadedBy",
                    column: x => x.UploadedBy,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DocumentAllowedRoles",
            columns: table => new
            {
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentAllowedRoles", x => new { x.DocumentId, x.RoleId });
                table.ForeignKey(
                    name: "FK_DocumentAllowedRoles_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_DocumentAllowedRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_Documents_AssetId", table: "Documents", column: "AssetId");
        migrationBuilder.CreateIndex(name: "IX_Documents_DocumentType", table: "Documents", column: "DocumentType");
        migrationBuilder.CreateIndex(name: "IX_Documents_Status", table: "Documents", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_Documents_Title", table: "Documents", column: "Title");
        migrationBuilder.CreateIndex(name: "IX_Documents_UploadedBy", table: "Documents", column: "UploadedBy");
        migrationBuilder.CreateIndex(name: "IX_DocumentAllowedRoles_RoleId", table: "DocumentAllowedRoles", column: "RoleId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DocumentAllowedRoles");
        migrationBuilder.DropTable(name: "Documents");
    }
}
