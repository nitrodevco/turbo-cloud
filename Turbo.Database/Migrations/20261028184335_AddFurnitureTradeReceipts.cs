using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFurnitureTradeReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "furniture_trade_receipts",
                    columns: table => new
                    {
                        operation_id = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        request_hash = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        initiator_id = table.Column<int>(type: "int", nullable: false),
                        partner_id = table.Column<int>(type: "int", nullable: false),
                        initiator_item_ids = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        partner_item_ids = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        initiator_reconciled = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false
                        ),
                        partner_reconciled = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false
                        ),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        reconcile_attempted_at = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: false
                        ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_furniture_trade_receipts", x => x.operation_id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_furniture_trade_receipts_initiator_id_initiator_reconciled",
                table: "furniture_trade_receipts",
                columns: new[] { "initiator_id", "initiator_reconciled" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_furniture_trade_receipts_initiator_reconciled_partner_reconc~",
                table: "furniture_trade_receipts",
                columns: new[]
                {
                    "initiator_reconciled",
                    "partner_reconciled",
                    "reconcile_attempted_at",
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_furniture_trade_receipts_partner_id_partner_reconciled",
                table: "furniture_trade_receipts",
                columns: new[] { "partner_id", "partner_reconciled" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "furniture_trade_receipts");
        }
    }
}
