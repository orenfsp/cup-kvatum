using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicantOutcomeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnCount",
                schema: "otklik",
                table: "Appeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnedAt",
                schema: "otklik",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppealComplaints",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientComplaintId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealComplaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealComplaints_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealComplaints_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealResultFeedbacks",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientFeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealResultFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealResultFeedbacks_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicantOutcomeActions",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ReturnReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReturnSequence = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantOutcomeActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicantOutcomeActions_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                schema: "otklik",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Key);
                    table.ForeignKey(
                        name: "FK_PlatformSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "otklik",
                table: "PlatformSettings",
                columns: new[] { "Key", "UpdatedAt", "UpdatedByUserId", "Value" },
                values: new object[] { "Appeal.AutoCloseDays", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "7" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealComplaints_AppealId",
                schema: "otklik",
                table: "AppealComplaints",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealComplaints_ClientComplaintId",
                schema: "otklik",
                table: "AppealComplaints",
                column: "ClientComplaintId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealComplaints_ResolvedAt_CreatedAt",
                schema: "otklik",
                table: "AppealComplaints",
                columns: new[] { "ResolvedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealComplaints_ResolvedByUserId",
                schema: "otklik",
                table: "AppealComplaints",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealResultFeedbacks_AppealId",
                schema: "otklik",
                table: "AppealResultFeedbacks",
                column: "AppealId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealResultFeedbacks_ClientFeedbackId",
                schema: "otklik",
                table: "AppealResultFeedbacks",
                column: "ClientFeedbackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantOutcomeActions_AppealId_CreatedAt",
                schema: "otklik",
                table: "ApplicantOutcomeActions",
                columns: new[] { "AppealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantOutcomeActions_ClientActionId",
                schema: "otklik",
                table: "ApplicantOutcomeActions",
                column: "ClientActionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_UpdatedByUserId",
                schema: "otklik",
                table: "PlatformSettings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealComplaints",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealResultFeedbacks",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "ApplicantOutcomeActions",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "PlatformSettings",
                schema: "otklik");

            migrationBuilder.DropColumn(
                name: "ReturnCount",
                schema: "otklik",
                table: "Appeals");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                schema: "otklik",
                table: "Appeals");
        }
    }
}
