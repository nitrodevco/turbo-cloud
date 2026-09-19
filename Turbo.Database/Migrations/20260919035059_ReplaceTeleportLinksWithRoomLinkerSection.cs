using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTeleportLinksWithRoomLinkerSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each linked item now names its pair in its own extra data ("room_linker" section),
            // so carry the existing pairs over, both directions, before the table goes.
            foreach (
                var (self, pair) in new[]
                {
                    ("furniture_one_id", "furniture_two_id"),
                    ("furniture_two_id", "furniture_one_id"),
                }
            )
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE furniture f
                    JOIN furniture_teleport_links l ON l.{self} = f.id AND l.deleted_at IS NULL
                    SET f.extra_data = JSON_SET(
                        CASE WHEN JSON_VALID(f.extra_data) THEN f.extra_data ELSE '{"{}"}' END,
                        '$.room_linker',
                        JSON_OBJECT('ItemId', l.{pair})
                    );
                    """
                );
            }

            migrationBuilder.DropTable(name: "furniture_teleport_links");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "furniture_teleport_links",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        furniture_one_id = table.Column<int>(type: "int", nullable: false),
                        furniture_two_id = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_furniture_teleport_links", x => x.id);
                        table.ForeignKey(
                            name: "FK_furniture_teleport_links_furniture_furniture_one_id",
                            column: x => x.furniture_one_id,
                            principalTable: "furniture",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_furniture_teleport_links_furniture_furniture_two_id",
                            column: x => x.furniture_two_id,
                            principalTable: "furniture",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_furniture_teleport_links_furniture_one_id",
                table: "furniture_teleport_links",
                column: "furniture_one_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_furniture_teleport_links_furniture_two_id",
                table: "furniture_teleport_links",
                column: "furniture_two_id",
                unique: true
            );
        }
    }
}
