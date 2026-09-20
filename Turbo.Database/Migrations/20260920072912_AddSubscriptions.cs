using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "subscription_days",
                table: "catalog_products",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "subscription_type",
                table: "catalog_products",
                type: "int",
                nullable: true
            );

            migrationBuilder
                .CreateTable(
                    name: "player_subscriptions",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        subscription_type = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        first_subscribed_at = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: true
                        ),
                        total_days_subscribed = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        periods_purchased = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        furni_limit = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
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
                        table.PrimaryKey("PK_player_subscriptions", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_subscriptions_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_subscriptions_player_id_subscription_type",
                table: "player_subscriptions",
                columns: new[] { "player_id", "subscription_type" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_subscriptions_subscription_type_expires_at",
                table: "player_subscriptions",
                columns: new[] { "subscription_type", "expires_at" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_subscriptions");

            migrationBuilder.DropColumn(name: "subscription_days", table: "catalog_products");

            migrationBuilder.DropColumn(name: "subscription_type", table: "catalog_products");
        }
    }
}
