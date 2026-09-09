using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorTriageRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssignedAt",
                schema: "otklik",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedExpertId",
                schema: "otklik",
                table: "Appeals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                schema: "otklik",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CrisisFlag",
                schema: "otklik",
                table: "Appeals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                schema: "otklik",
                table: "Appeals",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "PublicResolution",
                schema: "otklik",
                table: "Appeals",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionInternalReason",
                schema: "otklik",
                table: "Appeals",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "otklik",
                table: "Appeals",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "otklik",
                table: "Appeals",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AdminAlerts",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAlerts_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertGroups",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ActiveAppealLimit = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatorActionLogs",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FromValue = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorActionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorActionLogs_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorActionLogs_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealRoutingRules",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealRoutingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealRoutingRules_AppealCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "otklik",
                        principalTable: "AppealCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealRoutingRules_ExpertGroups_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalSchema: "otklik",
                        principalTable: "ExpertGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpertGroupMemberships",
                schema: "otklik",
                columns: table => new
                {
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertGroupMemberships", x => new { x.ExpertGroupId, x.ExpertUserId });
                    table.ForeignKey(
                        name: "FK_ExpertGroupMemberships_AspNetUsers_ExpertUserId",
                        column: x => x.ExpertUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpertGroupMemberships_ExpertGroups_ExpertGroupId",
                        column: x => x.ExpertGroupId,
                        principalSchema: "otklik",
                        principalTable: "ExpertGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "otklik",
                table: "ExpertGroups",
                columns: new[] { "Id", "ActiveAppealLimit", "Code", "DisplayName", "IsActive" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), 5, "support", "Психологическая поддержка", true },
                    { new Guid("30000000-0000-0000-0000-000000000002"), 5, "mediation", "Медиация конфликтов", true }
                });

            migrationBuilder.InsertData(
                schema: "otklik",
                table: "AppealRoutingRules",
                columns: new[] { "Id", "CategoryId", "ExpertGroupId" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), new Guid("30000000-0000-0000-0000-000000000001") },
                    { new Guid("40000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000003"), new Guid("30000000-0000-0000-0000-000000000001") },
                    { new Guid("40000000-0000-0000-0000-000000000003"), new Guid("20000000-0000-0000-0000-000000000004"), new Guid("30000000-0000-0000-0000-000000000001") },
                    { new Guid("40000000-0000-0000-0000-000000000004"), new Guid("20000000-0000-0000-0000-000000000002"), new Guid("30000000-0000-0000-0000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_AssignedExpertId",
                schema: "otklik",
                table: "Appeals",
                column: "AssignedExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_Status_CrisisFlag_Priority_CreatedAt",
                schema: "otklik",
                table: "Appeals",
                columns: new[] { "Status", "CrisisFlag", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlerts_AppealId_Type_ResolvedAt",
                schema: "otklik",
                table: "AdminAlerts",
                columns: new[] { "AppealId", "Type", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealRoutingRules_CategoryId",
                schema: "otklik",
                table: "AppealRoutingRules",
                column: "CategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealRoutingRules_ExpertGroupId",
                schema: "otklik",
                table: "AppealRoutingRules",
                column: "ExpertGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroupMemberships_ExpertUserId",
                schema: "otklik",
                table: "ExpertGroupMemberships",
                column: "ExpertUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertGroups_Code",
                schema: "otklik",
                table: "ExpertGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatorActionLogs_ActorUserId",
                schema: "otklik",
                table: "OperatorActionLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorActionLogs_AppealId_OccurredAt",
                schema: "otklik",
                table: "OperatorActionLogs",
                columns: new[] { "AppealId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appeals_AspNetUsers_AssignedExpertId",
                schema: "otklik",
                table: "Appeals",
                column: "AssignedExpertId",
                principalSchema: "otklik",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appeals_AspNetUsers_AssignedExpertId",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropTable(
                name: "AdminAlerts",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealRoutingRules",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "ExpertGroupMemberships",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "OperatorActionLogs",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "ExpertGroups",
                schema: "otklik");

            migrationBuilder.DropIndex(
                name: "IX_Appeals_AssignedExpertId",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropIndex(
                name: "IX_Appeals_Status_CrisisFlag_Priority_CreatedAt",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "AssignedExpertId",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "CrisisFlag",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "PublicResolution",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "RejectionInternalReason",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "otklik",
                table: "Appeals");
        }
    }
}
