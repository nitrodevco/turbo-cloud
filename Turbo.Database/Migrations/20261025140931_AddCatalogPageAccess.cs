using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogPageAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "min_rank",
                table: "catalog_pages",
                type: "int",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "required_node",
                    table: "catalog_pages",
                    type: "varchar(128)",
                    maxLength: 128,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "min_rank", table: "catalog_pages");

            migrationBuilder.DropColumn(name: "required_node", table: "catalog_pages");
        }
    }
}
