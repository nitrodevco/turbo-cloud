using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// The earnings vault's rows (<c>player_income_rewards</c>): what waits for each player per
    /// earnings category, unique per player and payer reference, kept once claimed.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261016150000_AddPlayerIncomeRewards")]
    public partial class AddPlayerIncomeRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "player_income_rewards",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        category = table.Column<int>(type: "int", nullable: false),
                        reward_type = table.Column<int>(type: "int", nullable: false),
                        amount = table.Column<int>(type: "int", nullable: false),
                        product_code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        reference = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        claim_reference = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        claimed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_income_rewards", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_income_rewards_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_income_rewards_player_id_reference",
                table: "player_income_rewards",
                columns: new[] { "player_id", "reference" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_income_rewards");
        }
    }
}
