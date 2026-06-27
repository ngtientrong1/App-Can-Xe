using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CanXe.Infrastructure.Migrations;

[Migration("202606270001_Phase16_AuditAndWeightOverride")]
public class Phase16_AuditAndWeightOverride : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "OverrideWeightGrams",
            table: "WeighEvents",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "IsManualOverride",
            table: "WeighEvents",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "OverrideReason",
            table: "WeighEvents",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OverrideAt",
            table: "WeighEvents",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OverrideBy",
            table: "WeighEvents",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                FieldName = table.Column<string>(type: "TEXT", nullable: false),
                OldValue = table.Column<string>(type: "TEXT", nullable: true),
                NewValue = table.Column<string>(type: "TEXT", nullable: true),
                Reason = table.Column<string>(type: "TEXT", nullable: true),
                EditedAt = table.Column<string>(type: "TEXT", nullable: false),
                EditedBy = table.Column<string>(type: "TEXT", nullable: true),
                IsDeveloperOverride = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TicketId",
            table: "AuditLogs",
            column: "TicketId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EditedAt",
            table: "AuditLogs",
            column: "EditedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditLogs");

        migrationBuilder.DropColumn(name: "OverrideWeightGrams", table: "WeighEvents");
        migrationBuilder.DropColumn(name: "IsManualOverride", table: "WeighEvents");
        migrationBuilder.DropColumn(name: "OverrideReason", table: "WeighEvents");
        migrationBuilder.DropColumn(name: "OverrideAt", table: "WeighEvents");
        migrationBuilder.DropColumn(name: "OverrideBy", table: "WeighEvents");
    }
}
