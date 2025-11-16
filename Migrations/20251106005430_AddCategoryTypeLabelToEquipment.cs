using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory_System.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryTypeLabelToEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Equipment",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Equipment",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Equipment",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Equipment",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Equipment");
        }
    }
}
