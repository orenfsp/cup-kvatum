using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealAttachments",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealAttachments_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealAttachments_AppealId_ClientUploadId",
                schema: "otklik",
                table: "AppealAttachments",
                columns: new[] { "AppealId", "ClientUploadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealAttachments_StorageKey",
                schema: "otklik",
                table: "AppealAttachments",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealAttachments",
                schema: "otklik");
        }
    }
}
