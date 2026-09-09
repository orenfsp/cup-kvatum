using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpertCommandIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientRecommendationId",
                schema: "otklik",
                table: "AppealRecommendations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ClientNoteId",
                schema: "otklik",
                table: "AppealInternalNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppealRecommendations_AppealId_ClientRecommendationId",
                schema: "otklik",
                table: "AppealRecommendations",
                columns: new[] { "AppealId", "ClientRecommendationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealInternalNotes_AppealId_ClientNoteId",
                schema: "otklik",
                table: "AppealInternalNotes",
                columns: new[] { "AppealId", "ClientNoteId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppealRecommendations_AppealId_ClientRecommendationId",
                schema: "otklik",
                table: "AppealRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_AppealInternalNotes_AppealId_ClientNoteId",
                schema: "otklik",
                table: "AppealInternalNotes");

            migrationBuilder.DropColumn(
                name: "ClientRecommendationId",
                schema: "otklik",
                table: "AppealRecommendations");

            migrationBuilder.DropColumn(
                name: "ClientNoteId",
                schema: "otklik",
                table: "AppealInternalNotes");
        }
    }
}
