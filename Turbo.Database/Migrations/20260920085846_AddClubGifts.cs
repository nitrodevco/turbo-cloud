using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddClubGifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "club_gift_days_required",
                table: "catalog_offers",
                type: "int",
                nullable: true
            );

            migrationBuilder
                .CreateTable(
                    name: "player_club_gifts",
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
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_club_gifts", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_club_gifts_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_club_gifts_player_id",
                table: "player_club_gifts",
                column: "player_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_club_gifts");

            migrationBuilder.DropColumn(name: "club_gift_days_required", table: "catalog_offers");
        }
    }
}
