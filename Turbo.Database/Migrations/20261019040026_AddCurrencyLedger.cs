using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "currency_ledger",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        outbox_id = table.Column<long>(type: "bigint", nullable: false),
                        operation_id = table
                            .Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        seq = table.Column<short>(type: "smallint", nullable: false),
                        occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        currency_type = table.Column<int>(type: "int", nullable: false),
                        activity_point_type = table.Column<int>(type: "int", nullable: true),
                        amount = table.Column<long>(type: "bigint", nullable: false),
                        cause = table
                            .Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        cause_ref = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        actor_id = table.Column<int>(type: "int", nullable: false),
                        other_kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                        other_owner_id = table.Column<int>(type: "int", nullable: false),
                        other_place_id = table.Column<long>(type: "bigint", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_currency_ledger", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "movement_consumer_cursors",
                    columns: table => new
                    {
                        name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        outbox_id = table.Column<long>(type: "bigint", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_movement_consumer_cursors", x => x.name);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_currency_ledger_operation_id_seq_player_id",
                table: "currency_ledger",
                columns: new[] { "operation_id", "seq", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_currency_ledger_player_id_id",
                table: "currency_ledger",
                columns: new[] { "player_id", "id" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "currency_ledger");

            migrationBuilder.DropTable(name: "movement_consumer_cursors");
        }
    }
}
