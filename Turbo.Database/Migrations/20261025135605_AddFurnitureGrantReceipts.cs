using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFurnitureGrantReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "furniture_grant_receipts",
                    columns: table => new
                    {
                        player_id = table.Column<int>(type: "int", nullable: false),
                        reference = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        furniture_id = table.Column<int>(type: "int", nullable: false),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_furniture_grant_receipts",
                            x => new { x.player_id, x.reference }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "furniture_grant_receipts");
        }
    }
}
