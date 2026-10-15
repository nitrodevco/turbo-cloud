using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Where each player is with each targeted offer (<c>player_targeted_offers</c>): the window
    /// state they left it in and how many they bought. The offers are configuration.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261014120000_AddPlayerTargetedOffers")]
    public partial class AddPlayerTargetedOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "player_targeted_offers",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        offer_id = table.Column<int>(type: "int", nullable: false),
                        tracking_state = table.Column<int>(type: "int", nullable: false),
                        purchased = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_targeted_offers", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_targeted_offers_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_targeted_offers_player_id_offer_id",
                table: "player_targeted_offers",
                columns: new[] { "player_id", "offer_id" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_targeted_offers");
        }
    }
}
