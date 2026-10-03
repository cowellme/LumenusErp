using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumenusErp.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class AddTaskOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_Source_ExternalId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_Source_Status_TitleNormalized",
                table: "TaskItems");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "TaskItems",
                type: "text",
                nullable: true);

            // Существующие задачи достаются администратору (первому по e-mail); без администратора владельца нет — задачи удаляем.
            migrationBuilder.Sql("""
                UPDATE "TaskItems" SET "OwnerId" = (
                    SELECT u."Id" FROM "AspNetUsers" u
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE r."Name" = 'Admin'
                    ORDER BY u."Email", u."Id"
                    LIMIT 1);
                DELETE FROM "TaskItems" WHERE "OwnerId" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "TaskItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "UserApiTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApiTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserApiTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_OwnerId_Source_ExternalId",
                table: "TaskItems",
                columns: new[] { "OwnerId", "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_OwnerId_Source_Status_TitleNormalized",
                table: "TaskItems",
                columns: new[] { "OwnerId", "Source", "Status", "TitleNormalized" });

            migrationBuilder.CreateIndex(
                name: "IX_UserApiTokens_TokenHash",
                table: "UserApiTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserApiTokens_UserId",
                table: "UserApiTokens",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_AspNetUsers_OwnerId",
                table: "TaskItems",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_AspNetUsers_OwnerId",
                table: "TaskItems");

            migrationBuilder.DropTable(
                name: "UserApiTokens");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_OwnerId_Source_ExternalId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_OwnerId_Source_Status_TitleNormalized",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "TaskItems");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_Source_ExternalId",
                table: "TaskItems",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_Source_Status_TitleNormalized",
                table: "TaskItems",
                columns: new[] { "Source", "Status", "TitleNormalized" });
        }
    }
}
