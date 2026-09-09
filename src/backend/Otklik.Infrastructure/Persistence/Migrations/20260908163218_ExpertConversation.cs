using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpertConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealInternalNotes",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealInternalNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealInternalNotes_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealInternalNotes_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealMessages",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Author = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealMessages_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealMessages_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealRecommendations",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealRecommendations_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealRecommendations_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealInternalNotes_AppealId_CreatedAt",
                schema: "otklik",
                table: "AppealInternalNotes",
                columns: new[] { "AppealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealInternalNotes_AuthorUserId",
                schema: "otklik",
                table: "AppealInternalNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealMessages_AppealId_ClientMessageId",
                schema: "otklik",
                table: "AppealMessages",
                columns: new[] { "AppealId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealMessages_AppealId_CreatedAt",
                schema: "otklik",
                table: "AppealMessages",
                columns: new[] { "AppealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealMessages_AuthorUserId",
                schema: "otklik",
                table: "AppealMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealRecommendations_AppealId_Version",
                schema: "otklik",
                table: "AppealRecommendations",
                columns: new[] { "AppealId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealRecommendations_AuthorUserId",
                schema: "otklik",
                table: "AppealRecommendations",
                column: "AuthorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealInternalNotes",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealMessages",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealRecommendations",
                schema: "otklik");
        }
    }
}
