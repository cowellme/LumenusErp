using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumenusErp.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class TaskDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "TaskItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartAt",
                table: "TaskItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "CallTaskSuggestions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartAt",
                table: "CallTaskSuggestions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_OwnerId_Status_DueAt",
                table: "TaskItems",
                columns: new[] { "OwnerId", "Status", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_OwnerId_Status_DueAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "CallTaskSuggestions");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "CallTaskSuggestions");
        }
    }
}
