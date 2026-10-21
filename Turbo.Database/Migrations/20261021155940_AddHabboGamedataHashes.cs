using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHabboGamedataHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<string>(
                    name: "habbo_hash",
                    table: "habbo_variable_versions",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AddColumn<string>(
                    name: "habbo_hash",
                    table: "habbo_text_versions",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AddColumn<string>(
                    name: "habbo_hash",
                    table: "habbo_releases",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AddColumn<string>(
                    name: "habbo_hash",
                    table: "habbo_product_versions",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AddColumn<string>(
                    name: "habbo_hash",
                    table: "habbo_figure_versions",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "habbo_hash", table: "habbo_variable_versions");

            migrationBuilder.DropColumn(name: "habbo_hash", table: "habbo_text_versions");

            migrationBuilder.DropColumn(name: "habbo_hash", table: "habbo_releases");

            migrationBuilder.DropColumn(name: "habbo_hash", table: "habbo_product_versions");

            migrationBuilder.DropColumn(name: "habbo_hash", table: "habbo_figure_versions");
        }
    }
}
