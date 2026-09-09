using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PwaDeviceCapability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealDeviceSessions",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealDeviceSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppealNotificationOutboxes",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealNotificationOutboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealNotificationOutboxes_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppealDeviceGrants",
                schema: "otklik",
                columns: table => new
                {
                    DeviceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealDeviceGrants", x => new { x.DeviceSessionId, x.AppealId });
                    table.ForeignKey(
                        name: "FK_AppealDeviceGrants_AppealDeviceSessions_DeviceSessionId",
                        column: x => x.DeviceSessionId,
                        principalSchema: "otklik",
                        principalTable: "AppealDeviceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealDeviceGrants_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppealPushSubscriptions",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    EndpointCiphertext = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    P256dhCiphertext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AuthCiphertext = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastDeliveryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealPushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealPushSubscriptions_AppealDeviceSessions_DeviceSessionId",
                        column: x => x.DeviceSessionId,
                        principalSchema: "otklik",
                        principalTable: "AppealDeviceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealPushSubscriptions_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealDeviceGrants_AppealId_RevokedAt",
                schema: "otklik",
                table: "AppealDeviceGrants",
                columns: new[] { "AppealId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealDeviceSessions_RevokedAt_ExpiresAt",
                schema: "otklik",
                table: "AppealDeviceSessions",
                columns: new[] { "RevokedAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealDeviceSessions_TokenHash",
                schema: "otklik",
                table: "AppealDeviceSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealNotificationOutboxes_AppealId",
                schema: "otklik",
                table: "AppealNotificationOutboxes",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealNotificationOutboxes_ProcessedAt_OccurredAt",
                schema: "otklik",
                table: "AppealNotificationOutboxes",
                columns: new[] { "ProcessedAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealPushSubscriptions_AppealId_RevokedAt",
                schema: "otklik",
                table: "AppealPushSubscriptions",
                columns: new[] { "AppealId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealPushSubscriptions_DeviceSessionId",
                schema: "otklik",
                table: "AppealPushSubscriptions",
                column: "DeviceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealPushSubscriptions_EndpointHash",
                schema: "otklik",
                table: "AppealPushSubscriptions",
                column: "EndpointHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealDeviceGrants",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealNotificationOutboxes",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealPushSubscriptions",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealDeviceSessions",
                schema: "otklik");
        }
    }
}
