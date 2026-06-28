using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Company.Orchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCancellationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Jobs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelRequestedAtUtc",
                table: "Jobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "Jobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "Jobs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CancelRequestedAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "Jobs");
        }
    }
}
