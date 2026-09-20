using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildersClubFurniture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "builders_club_furniture",
                    columns: table => new
                    {
                        room_id = table.Column<int>(type: "int", nullable: false),
                        room_object_id = table.Column<int>(type: "int", nullable: false),
                        definition_id = table.Column<int>(type: "int", nullable: false),
                        placed_by_player_id = table.Column<int>(type: "int", nullable: false),
                        offer_id = table.Column<int>(type: "int", nullable: false),
                        x = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        y = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        z = table.Column<double>(
                            type: "double(10,3)",
                            nullable: false,
                            defaultValue: 0.0
                        ),
                        direction = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        wall_offset = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        extra_data = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_builders_club_furniture",
                            x => new { x.room_id, x.room_object_id }
                        );
                        table.ForeignKey(
                            name: "FK_builders_club_furniture_furniture_definitions_definition_id",
                            column: x => x.definition_id,
                            principalTable: "furniture_definitions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_builders_club_furniture_players_placed_by_player_id",
                            column: x => x.placed_by_player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_builders_club_furniture_rooms_room_id",
                            column: x => x.room_id,
                            principalTable: "rooms",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_builders_club_furniture_definition_id",
                table: "builders_club_furniture",
                column: "definition_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_builders_club_furniture_placed_by_player_id",
                table: "builders_club_furniture",
                column: "placed_by_player_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "builders_club_furniture");
        }
    }
}
