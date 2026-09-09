using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CrisisFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CrisisDetectedAt",
                schema: "otklik",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppealCrisisContacts",
                schema: "otklik",
                columns: table => new
                {
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ciphertext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealCrisisContacts", x => x.AppealId);
                    table.ForeignKey(
                        name: "FK_AppealCrisisContacts_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrisisContactAccessLogs",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrisisContactAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrisisContactAccessLogs_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrisisContactAccessLogs_AspNetUsers_OperatorUserId",
                        column: x => x.OperatorUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CrisisMarkers",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RiskType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrisisMarkers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrisisSupportContacts",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DisplayNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DialNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrisisSupportContacts", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "otklik",
                table: "CrisisMarkers",
                columns: new[] { "Id", "IsActive", "Pattern", "RiskType", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000001"), true, "меня избива*", "PhysicalViolence", 10 },
                    { new Guid("60000000-0000-0000-0000-000000000002"), true, "меня бьют", "PhysicalViolence", 20 },
                    { new Guid("60000000-0000-0000-0000-000000000003"), true, "ударил* меня", "PhysicalViolence", 30 },
                    { new Guid("60000000-0000-0000-0000-000000000004"), true, "угрожа* уб*", "LifeThreat", 40 },
                    { new Guid("60000000-0000-0000-0000-000000000005"), true, "меня убьют", "LifeThreat", 50 },
                    { new Guid("60000000-0000-0000-0000-000000000006"), true, "напал* нож*", "LifeThreat", 60 },
                    { new Guid("60000000-0000-0000-0000-000000000007"), true, "хочу умер*", "SuicideRisk", 70 },
                    { new Guid("60000000-0000-0000-0000-000000000008"), true, "не хочу жить", "SuicideRisk", 80 },
                    { new Guid("60000000-0000-0000-0000-000000000009"), true, "покончи* с собой", "SuicideRisk", 90 },
                    { new Guid("60000000-0000-0000-0000-000000000010"), true, "убью себя", "SuicideRisk", 100 },
                    { new Guid("60000000-0000-0000-0000-000000000011"), true, "самоубий*", "SuicideRisk", 110 },
                    { new Guid("60000000-0000-0000-0000-000000000012"), true, "суицид*", "SuicideRisk", 120 }
                });

            migrationBuilder.InsertData(
                schema: "otklik",
                table: "CrisisSupportContacts",
                columns: new[] { "Id", "Description", "DialNumber", "DisplayName", "DisplayNumber", "IsActive", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("61000000-0000-0000-0000-000000000001"), "Бесплатно и анонимно по России", "88002000122", "Детский телефон доверия", "8-800-2000-122", true, 10 },
                    { new Guid("61000000-0000-0000-0000-000000000002"), "С мобильного телефона", "124", "Короткий номер телефона доверия", "124", true, 20 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrisisContactAccessLogs_AppealId_AccessedAt",
                schema: "otklik",
                table: "CrisisContactAccessLogs",
                columns: new[] { "AppealId", "AccessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrisisContactAccessLogs_OperatorUserId",
                schema: "otklik",
                table: "CrisisContactAccessLogs",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrisisMarkers_IsActive_SortOrder",
                schema: "otklik",
                table: "CrisisMarkers",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CrisisMarkers_Pattern",
                schema: "otklik",
                table: "CrisisMarkers",
                column: "Pattern",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrisisSupportContacts_IsActive_SortOrder",
                schema: "otklik",
                table: "CrisisSupportContacts",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealCrisisContacts",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "CrisisContactAccessLogs",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "CrisisMarkers",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "CrisisSupportContacts",
                schema: "otklik");

            migrationBuilder.DropColumn(
                name: "CrisisDetectedAt",
                schema: "otklik",
                table: "Appeals");
        }
    }
}
