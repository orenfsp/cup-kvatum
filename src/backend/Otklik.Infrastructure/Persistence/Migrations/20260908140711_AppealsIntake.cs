using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppealsIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealCategories",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appeals",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmissionPath = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Narrative = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TrackHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    TrackRecoveryCiphertext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appeals_AppealCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "otklik",
                        principalTable: "AppealCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealAnswers",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealAnswers_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppealStatusChanges",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealStatusChanges_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "otklik",
                table: "AppealCategories",
                columns: new[] { "Id", "Code", "DisplayName", "IsActive", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "bullying", "Травля", true, 10 },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "conflict", "Конфликт", true, 20 },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "pressure", "Давление", true, 30 },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "unsure", "Не знаю, как это назвать", true, 40 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealAnswers_AppealId_QuestionCode",
                schema: "otklik",
                table: "AppealAnswers",
                columns: new[] { "AppealId", "QuestionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealCategories_Code",
                schema: "otklik",
                table: "AppealCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_CategoryId",
                schema: "otklik",
                table: "Appeals",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_ClientRequestId",
                schema: "otklik",
                table: "Appeals",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_TrackHash",
                schema: "otklik",
                table: "Appeals",
                column: "TrackHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealStatusChanges_AppealId_ChangedAt",
                schema: "otklik",
                table: "AppealStatusChanges",
                columns: new[] { "AppealId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealAnswers",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealStatusChanges",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "Appeals",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealCategories",
                schema: "otklik");
        }
    }
}
