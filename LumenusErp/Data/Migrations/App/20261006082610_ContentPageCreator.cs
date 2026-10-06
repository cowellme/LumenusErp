using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumenusErp.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class ContentPageCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadedById",
                table: "MediaFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "ContentPages",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_UploadedById",
                table: "MediaFiles",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPages_CreatedById",
                table: "ContentPages",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentPages_AspNetUsers_CreatedById",
                table: "ContentPages",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaFiles_AspNetUsers_UploadedById",
                table: "MediaFiles",
                column: "UploadedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentPages_AspNetUsers_CreatedById",
                table: "ContentPages");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaFiles_AspNetUsers_UploadedById",
                table: "MediaFiles");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_UploadedById",
                table: "MediaFiles");

            migrationBuilder.DropIndex(
                name: "IX_ContentPages_CreatedById",
                table: "ContentPages");

            migrationBuilder.DropColumn(
                name: "UploadedById",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ContentPages");
        }
    }
}
