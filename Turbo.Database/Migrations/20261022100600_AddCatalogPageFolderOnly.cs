using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogPageFolderOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "folder_only",
                table: "catalog_pages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "folder_only", table: "catalog_pages");
        }
    }
}
