using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminConfigurationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "otklik",
                table: "ExpertGroups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTimeOffset.UnixEpoch);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "otklik",
                table: "ExpertGroups",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                schema: "otklik",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "otklik",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTimeOffset.UnixEpoch);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedExpertGroupId",
                schema: "otklik",
                table: "Appeals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppliedRoutingRuleId",
                schema: "otklik",
                table: "Appeals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliedRoutingRuleVersion",
                schema: "otklik",
                table: "Appeals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "otklik",
                table: "AppealRoutingRules",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "otklik",
                table: "AppealRoutingRules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTimeOffset.UnixEpoch);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "otklik",
                table: "AppealRoutingRules",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "otklik",
                table: "AppealCategories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTimeOffset.UnixEpoch);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "otklik",
                table: "AppealCategories",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AdministrativeAuditEvents",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeforeMetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterMetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdministrativeAuditEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealCategories",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                columns: new[] { "UpdatedAt", "Version" },
                values: new object[] { new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealCategories",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                columns: new[] { "UpdatedAt", "Version" },
                values: new object[] { new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealCategories",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "UpdatedAt", "Version" },
                values: new object[] { new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealCategories",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                columns: new[] { "UpdatedAt", "Version" },
                values: new object[] { new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealRoutingRules",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                columns: new[] { "IsActive", "UpdatedAt", "Version" },
                values: new object[] { true, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealRoutingRules",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                columns: new[] { "IsActive", "UpdatedAt", "Version" },
                values: new object[] { true, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealRoutingRules",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                columns: new[] { "IsActive", "UpdatedAt", "Version" },
                values: new object[] { true, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "AppealRoutingRules",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                columns: new[] { "IsActive", "UpdatedAt", "Version" },
                values: new object[] { true, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "ExpertGroups",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                columns: new[] { "UpdatedAt", "Version" },
                values: new object[] { new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                schema: "otklik",
                table: "ExpertGroups",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                columns: new[] { "UpdatedAt", "Version" },
                values: new object[] { new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_ActorUserId",
                schema: "otklik",
                table: "AdministrativeAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_OccurredAt_Action",
                schema: "otklik",
                table: "AdministrativeAuditEvents",
                columns: new[] { "OccurredAt", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_TargetType_TargetId",
                schema: "otklik",
                table: "AdministrativeAuditEvents",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministrativeAuditEvents",
                schema: "otklik");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "otklik",
                table: "ExpertGroups");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "otklik",
                table: "ExpertGroups");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                schema: "otklik",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "otklik",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AppliedExpertGroupId",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "AppliedRoutingRuleId",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "AppliedRoutingRuleVersion",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "otklik",
                table: "AppealRoutingRules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "otklik",
                table: "AppealRoutingRules");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "otklik",
                table: "AppealRoutingRules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "otklik",
                table: "AppealCategories");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "otklik",
                table: "AppealCategories");
        }
    }
}
