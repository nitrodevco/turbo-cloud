using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerOnlineRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "player_online_rewards",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        currency_type_id = table.Column<int>(type: "int", nullable: false),
                        day = table.Column<DateOnly>(type: "date", nullable: false),
                        amount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_online_rewards", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_online_rewards_currency_types_currency_type_id",
                            column: x => x.currency_type_id,
                            principalTable: "currency_types",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_player_online_rewards_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_online_rewards_currency_type_id",
                table: "player_online_rewards",
                column: "currency_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_online_rewards_player_id_currency_type_id",
                table: "player_online_rewards",
                columns: new[] { "player_id", "currency_type_id" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_online_rewards");
        }
    }
}
