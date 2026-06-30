using System;
using FactoryPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260630220000_AddProcessTags")]
public partial class AddProcessTags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProcessTags",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TagCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                Area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                EquipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                EquipmentName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DataType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Value = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                Quality = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PLCAddress = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                RefreshInterval = table.Column<int>(type: "integer", nullable: false),
                LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ProcessTags", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_ProcessTags_Area", table: "ProcessTags", column: "Area");
        migrationBuilder.CreateIndex(name: "IX_ProcessTags_Category", table: "ProcessTags", column: "Category");
        migrationBuilder.CreateIndex(name: "IX_ProcessTags_TagCode", table: "ProcessTags", column: "TagCode", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProcessTags");
    }
}
