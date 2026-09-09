using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticsPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyticsExports",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PeriodFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DownloadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsExports_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsOutboxMessages",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealVersion = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastPublishError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppealAnalyticsProjections",
                schema: "otklik",
                columns: table => new
                {
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpertGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Priority = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OperatorAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstExpertResponseAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OperatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedExpertId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastAssignedExpertId = table.Column<Guid>(type: "uuid", nullable: true),
                    CrisisFlag = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnCount = table.Column<int>(type: "integer", nullable: false),
                    LastAppealVersion = table.Column<int>(type: "integer", nullable: false),
                    LastEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealAnalyticsProjections", x => x.AppealId);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedAnalyticsEvents",
                schema: "otklik",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedAnalyticsEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsExports_RequestedByUserId_CreatedAt",
                schema: "otklik",
                table: "AnalyticsExports",
                columns: new[] { "RequestedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsOutboxMessages_AppealId_AppealVersion",
                schema: "otklik",
                table: "AnalyticsOutboxMessages",
                columns: new[] { "AppealId", "AppealVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsOutboxMessages_PublishedAt_OccurredAt",
                schema: "otklik",
                table: "AnalyticsOutboxMessages",
                columns: new[] { "PublishedAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealAnalyticsProjections_CreatedAt",
                schema: "otklik",
                table: "AppealAnalyticsProjections",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppealAnalyticsProjections_LastAssignedExpertId",
                schema: "otklik",
                table: "AppealAnalyticsProjections",
                column: "LastAssignedExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealAnalyticsProjections_OperatorUserId",
                schema: "otklik",
                table: "AppealAnalyticsProjections",
                column: "OperatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealAnalyticsProjections_Status_Priority",
                schema: "otklik",
                table: "AppealAnalyticsProjections",
                columns: new[] { "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedAnalyticsEvents_AppealId_ProcessedAt",
                schema: "otklik",
                table: "ProcessedAnalyticsEvents",
                columns: new[] { "AppealId", "ProcessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsExports",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AnalyticsOutboxMessages",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealAnalyticsProjections",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "ProcessedAnalyticsEvents",
                schema: "otklik");
        }
    }
}
