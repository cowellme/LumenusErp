using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumenusErp.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class AddBpmnFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BpmnFixed",
                table: "ContentBlocks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BpmnFixed",
                table: "ContentBlocks");
        }
    }
}
