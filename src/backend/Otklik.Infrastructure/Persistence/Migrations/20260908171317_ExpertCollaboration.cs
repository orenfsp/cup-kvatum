using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Otklik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpertCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealExpertParticipants",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealExpertParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealExpertParticipants_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealExpertParticipants_AspNetUsers_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealExpertParticipants_AspNetUsers_ExpertUserId",
                        column: x => x.ExpertUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealExpertParticipants_AspNetUsers_RemovedByUserId",
                        column: x => x.RemovedByUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpertWorkflowRequests",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SelectedExpertUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedPriority = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    KeepPreviousAsCoExecutor = table.Column<bool>(type: "boolean", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertWorkflowRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertWorkflowRequests_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpertWorkflowRequests_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpertWorkflowRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpertWorkflowRequests_AspNetUsers_SelectedExpertUserId",
                        column: x => x.SelectedExpertUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealAssignmentEvents",
                schema: "otklik",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpertUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealAssignmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealAssignmentEvents_Appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "otklik",
                        principalTable: "Appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealAssignmentEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealAssignmentEvents_AspNetUsers_ExpertUserId",
                        column: x => x.ExpertUserId,
                        principalSchema: "otklik",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppealAssignmentEvents_ExpertWorkflowRequests_WorkflowReque~",
                        column: x => x.WorkflowRequestId,
                        principalSchema: "otklik",
                        principalTable: "ExpertWorkflowRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealAssignmentEvents_ActorUserId",
                schema: "otklik",
                table: "AppealAssignmentEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealAssignmentEvents_AppealId_OccurredAt",
                schema: "otklik",
                table: "AppealAssignmentEvents",
                columns: new[] { "AppealId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealAssignmentEvents_ExpertUserId",
                schema: "otklik",
                table: "AppealAssignmentEvents",
                column: "ExpertUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealAssignmentEvents_WorkflowRequestId",
                schema: "otklik",
                table: "AppealAssignmentEvents",
                column: "WorkflowRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealExpertParticipants_AddedByUserId",
                schema: "otklik",
                table: "AppealExpertParticipants",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealExpertParticipants_AppealId_ExpertUserId_RemovedAt",
                schema: "otklik",
                table: "AppealExpertParticipants",
                columns: new[] { "AppealId", "ExpertUserId", "RemovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealExpertParticipants_ExpertUserId",
                schema: "otklik",
                table: "AppealExpertParticipants",
                column: "ExpertUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealExpertParticipants_RemovedByUserId",
                schema: "otklik",
                table: "AppealExpertParticipants",
                column: "RemovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkflowRequests_AppealId",
                schema: "otklik",
                table: "ExpertWorkflowRequests",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkflowRequests_ClientRequestId",
                schema: "otklik",
                table: "ExpertWorkflowRequests",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkflowRequests_DecidedByUserId",
                schema: "otklik",
                table: "ExpertWorkflowRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkflowRequests_RequestedByUserId",
                schema: "otklik",
                table: "ExpertWorkflowRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkflowRequests_SelectedExpertUserId",
                schema: "otklik",
                table: "ExpertWorkflowRequests",
                column: "SelectedExpertUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertWorkflowRequests_Status_RequestedAt",
                schema: "otklik",
                table: "ExpertWorkflowRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.Sql(
                """
                INSERT INTO otklik."AppealExpertParticipants"
                    ("Id", "AppealId", "ExpertUserId", "Role", "AddedByUserId", "AddedAt")
                SELECT gen_random_uuid(), appeal."Id", appeal."AssignedExpertId", 'Responsible',
                    COALESCE((
                        SELECT action."ActorUserId"
                        FROM otklik."OperatorActionLogs" action
                        WHERE action."AppealId" = appeal."Id" AND action."Action" = 'Assigned'
                        ORDER BY action."OccurredAt" DESC
                        LIMIT 1
                    ), appeal."AssignedExpertId"),
                    COALESCE(appeal."AssignedAt", appeal."CreatedAt")
                FROM otklik."Appeals" appeal
                WHERE appeal."AssignedExpertId" IS NOT NULL;

                INSERT INTO otklik."AppealAssignmentEvents"
                    ("Id", "AppealId", "EventType", "ExpertUserId", "Role", "ActorUserId", "OccurredAt")
                SELECT gen_random_uuid(), appeal."Id", 'Assigned', appeal."AssignedExpertId", 'Responsible',
                    COALESCE((
                        SELECT action."ActorUserId"
                        FROM otklik."OperatorActionLogs" action
                        WHERE action."AppealId" = appeal."Id" AND action."Action" = 'Assigned'
                        ORDER BY action."OccurredAt" DESC
                        LIMIT 1
                    ), appeal."AssignedExpertId"),
                    COALESCE(appeal."AssignedAt", appeal."CreatedAt")
                FROM otklik."Appeals" appeal
                WHERE appeal."AssignedExpertId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealAssignmentEvents",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "AppealExpertParticipants",
                schema: "otklik");

            migrationBuilder.DropTable(
                name: "ExpertWorkflowRequests",
                schema: "otklik");
        }
    }
}
