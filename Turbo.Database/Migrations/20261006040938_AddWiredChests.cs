using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWiredChests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "chest_item_id",
                table: "furniture",
                type: "int",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "chest_transaction_id",
                table: "furniture",
                type: "bigint",
                nullable: true
            );

            migrationBuilder
                .CreateTable(
                    name: "wired_chest_transactions",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        room_id = table.Column<int>(type: "int", nullable: false),
                        type = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        player_name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        definition_info = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_wired_chest_transactions", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "wired_chests",
                    columns: table => new
                    {
                        item_id = table.Column<int>(type: "int", nullable: false),
                        coins = table.Column<int>(type: "int", nullable: false),
                        capacity_level = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_wired_chests", x => x.item_id);
                        table.ForeignKey(
                            name: "FK_wired_chests_furniture_item_id",
                            column: x => x.item_id,
                            principalTable: "furniture",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "wired_chest_transaction_entries",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        transaction_id = table.Column<long>(type: "bigint", nullable: false),
                        chest_item_id = table.Column<int>(type: "int", nullable: false),
                        is_deposit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        is_coins = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        is_wall_item = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        type_id = table.Column<int>(type: "int", nullable: false),
                        poster_id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        count = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_wired_chest_transaction_entries", x => x.id);
                        table.ForeignKey(
                            name: "FK_wired_chest_transaction_entries_wired_chest_transactions_tra~",
                            column: x => x.transaction_id,
                            principalTable: "wired_chest_transactions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_furniture_chest_item_id",
                table: "furniture",
                column: "chest_item_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_wired_chest_transaction_entries_chest_item_id_transaction_id",
                table: "wired_chest_transaction_entries",
                columns: new[] { "chest_item_id", "transaction_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_wired_chest_transaction_entries_transaction_id",
                table: "wired_chest_transaction_entries",
                column: "transaction_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_wired_chest_transactions_room_id_id",
                table: "wired_chest_transactions",
                columns: new[] { "room_id", "id" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_furniture_furniture_chest_item_id",
                table: "furniture",
                column: "chest_item_id",
                principalTable: "furniture",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull
            );

            // The stock chests and contracts, which stood as plain furni until now.
            foreach (var (logic, pattern) in STOCK_LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE furniture_definitions SET logic = '{logic}' WHERE logic = 'default_floor' AND name LIKE '{pattern}';"
                );
        }

        private static readonly (string Logic, string Pattern)[] STOCK_LOGICS =
        [
            ("wired_chest_furni", "wf_storage_furni%"),
            ("wired_chest_coins", "wf_storage_coins%"),
            ("wired_contract_payment", "wf_contract_payment"),
            ("wired_contract_trade", "wf_contract_trade"),
            ("wired_contract_reward", "wf_contract_reward"),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (logic, _) in STOCK_LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE furniture_definitions SET logic = 'default_floor' WHERE logic = '{logic}';"
                );

            migrationBuilder.DropForeignKey(
                name: "FK_furniture_furniture_chest_item_id",
                table: "furniture"
            );

            migrationBuilder.DropTable(name: "wired_chest_transaction_entries");

            migrationBuilder.DropTable(name: "wired_chests");

            migrationBuilder.DropTable(name: "wired_chest_transactions");

            migrationBuilder.DropIndex(name: "IX_furniture_chest_item_id", table: "furniture");

            migrationBuilder.DropColumn(name: "chest_item_id", table: "furniture");

            migrationBuilder.DropColumn(name: "chest_transaction_id", table: "furniture");
        }
    }
}
