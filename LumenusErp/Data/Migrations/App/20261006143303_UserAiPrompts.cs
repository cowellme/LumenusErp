using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LumenusErp.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class UserAiPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromptSource",
                table: "CallRecordings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromptUpdatedAt",
                table: "CallRecordings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserAiPrompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SystemPrompt = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAiPrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAiPrompts_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAiPrompts_OwnerId_Key",
                table: "UserAiPrompts",
                columns: new[] { "OwnerId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAiPrompts");

            migrationBuilder.DropColumn(
                name: "PromptSource",
                table: "CallRecordings");

            migrationBuilder.DropColumn(
                name: "PromptUpdatedAt",
                table: "CallRecordings");
        }
    }
}
