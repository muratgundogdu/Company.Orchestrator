using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Company.Orchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderWatcherTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigJson",
                table: "Triggers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TriggerEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriggerEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriggerEvents_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TriggerEvents_Triggers_TriggerId",
                        column: x => x.TriggerId,
                        principalTable: "Triggers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TriggerEvents_ProcessInstanceId",
                table: "TriggerEvents",
                column: "ProcessInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TriggerEvents_Status",
                table: "TriggerEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TriggerEvents_TriggerId",
                table: "TriggerEvents",
                column: "TriggerId");

            migrationBuilder.CreateIndex(
                name: "IX_TriggerEvents_TriggerId_EventKey",
                table: "TriggerEvents",
                columns: new[] { "TriggerId", "EventKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriggerEvents");

            migrationBuilder.DropColumn(
                name: "ConfigJson",
                table: "Triggers");
        }
    }
}
