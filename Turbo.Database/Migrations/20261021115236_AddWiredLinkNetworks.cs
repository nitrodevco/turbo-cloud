using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWiredLinkNetworks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "wired_link_networks",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        name = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_wired_link_networks", x => x.id);
                        table.ForeignKey(
                            name: "FK_wired_link_networks_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "wired_link_endpoints",
                    columns: table => new
                    {
                        room_id = table.Column<int>(type: "int", nullable: false),
                        furni_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        network_id = table.Column<int>(type: "int", nullable: true),
                        receive_local = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        builders_club = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_wired_link_endpoints",
                            x => new { x.room_id, x.furni_id }
                        );
                        table.ForeignKey(
                            name: "FK_wired_link_endpoints_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_wired_link_endpoints_rooms_room_id",
                            column: x => x.room_id,
                            principalTable: "rooms",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_wired_link_endpoints_wired_link_networks_network_id",
                            column: x => x.network_id,
                            principalTable: "wired_link_networks",
                            principalColumn: "id",
                            onDelete: ReferentialAction.SetNull
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_wired_link_endpoints_network_id",
                table: "wired_link_endpoints",
                column: "network_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_wired_link_endpoints_player_id",
                table: "wired_link_endpoints",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_wired_link_networks_player_id",
                table: "wired_link_networks",
                column: "player_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "wired_link_endpoints");

            migrationBuilder.DropTable(name: "wired_link_networks");
        }
    }
}
