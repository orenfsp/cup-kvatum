using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations;

public partial class AppealThreadsAndCycles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AppealDeviceGrants_Appeals_AppealId",
            schema: "otklik",
            table: "AppealDeviceGrants");
        migrationBuilder.DropForeignKey(
            name: "FK_AppealPushSubscriptions_Appeals_AppealId",
            schema: "otklik",
            table: "AppealPushSubscriptions");

        migrationBuilder.RenameColumn(
            name: "AppealId", schema: "otklik", table: "AppealPushSubscriptions", newName: "ThreadId");
        migrationBuilder.RenameIndex(
            name: "IX_AppealPushSubscriptions_AppealId_RevokedAt",
            schema: "otklik",
            table: "AppealPushSubscriptions",
            newName: "IX_AppealPushSubscriptions_ThreadId_RevokedAt");
        migrationBuilder.RenameColumn(
            name: "AppealId", schema: "otklik", table: "AppealDeviceGrants", newName: "ThreadId");
        migrationBuilder.RenameIndex(
            name: "IX_AppealDeviceGrants_AppealId_RevokedAt",
            schema: "otklik",
            table: "AppealDeviceGrants",
            newName: "IX_AppealDeviceGrants_ThreadId_RevokedAt");

        migrationBuilder.AddColumn<Guid>(
            name: "ClientContinuationId", schema: "otklik", table: "Appeals", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "Sequence", schema: "otklik", table: "Appeals", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<Guid>(
            name: "ThreadId", schema: "otklik", table: "Appeals", type: "uuid", nullable: true);

        migrationBuilder.CreateTable(
            name: "AppealThreads",
            schema: "otklik",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TrackHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                TrackRecoveryCiphertext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                CurrentCycleId = table.Column<Guid>(type: "uuid", nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppealThreads", x => x.Id);
                table.ForeignKey(
                    name: "FK_AppealThreads_Appeals_CurrentCycleId",
                    column: x => x.CurrentCycleId,
                    principalSchema: "otklik",
                    principalTable: "Appeals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("""
            INSERT INTO otklik."AppealThreads"
                ("Id", "TrackHash", "TrackRecoveryCiphertext", "CurrentCycleId", "Version", "CreatedAt", "LastActivityAt")
            SELECT
                "Id", "TrackHash", "TrackRecoveryCiphertext", "Id", 1, "CreatedAt",
                COALESCE("CompletedAt", "ReturnedAt", "AssignedAt", "CreatedAt")
            FROM otklik."Appeals";

            UPDATE otklik."Appeals" SET "ThreadId" = "Id";
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "ThreadId",
            schema: "otklik",
            table: "Appeals",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.DropIndex(name: "IX_Appeals_TrackHash", schema: "otklik", table: "Appeals");
        migrationBuilder.DropColumn(name: "TrackHash", schema: "otklik", table: "Appeals");
        migrationBuilder.DropColumn(name: "TrackRecoveryCiphertext", schema: "otklik", table: "Appeals");

        migrationBuilder.CreateIndex(
            name: "IX_Appeals_ThreadId_ClientContinuationId",
            schema: "otklik",
            table: "Appeals",
            columns: new[] { "ThreadId", "ClientContinuationId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Appeals_ThreadId_Sequence",
            schema: "otklik",
            table: "Appeals",
            columns: new[] { "ThreadId", "Sequence" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_AppealThreads_CurrentCycleId",
            schema: "otklik",
            table: "AppealThreads",
            column: "CurrentCycleId");
        migrationBuilder.CreateIndex(
            name: "IX_AppealThreads_TrackHash",
            schema: "otklik",
            table: "AppealThreads",
            column: "TrackHash",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_AppealDeviceGrants_AppealThreads_ThreadId",
            schema: "otklik",
            table: "AppealDeviceGrants",
            column: "ThreadId",
            principalSchema: "otklik",
            principalTable: "AppealThreads",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(
            name: "FK_AppealPushSubscriptions_AppealThreads_ThreadId",
            schema: "otklik",
            table: "AppealPushSubscriptions",
            column: "ThreadId",
            principalSchema: "otklik",
            principalTable: "AppealThreads",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(
            name: "FK_Appeals_AppealThreads_ThreadId",
            schema: "otklik",
            table: "Appeals",
            column: "ThreadId",
            principalSchema: "otklik",
            principalTable: "AppealThreads",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Appeal cycles cannot be collapsed safely after continuation cycles have been created.");
}
