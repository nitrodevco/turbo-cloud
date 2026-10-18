using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// The receipts of reversals asked through <c>IAssetReversalService</c>
    /// (<c>asset_reversal_receipts</c>): one row per reversal operation, its id unique, so a retry
    /// finds the move already made. Empty until a plugin reverses something.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018220000_AddAssetReversalReceipts")]
    public partial class AddAssetReversalReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "asset_reversal_receipts",
                    columns: table => new
                    {
                        id = table
                            .Column<long>(type: "bigint", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        operation_id = table
                            .Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        reversed_operation_id = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        actor = table.Column<int>(type: "int", nullable: false),
                        reason = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        asset_kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                        asset_id = table.Column<long>(type: "bigint", nullable: false),
                        outcome = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_reversal_receipts", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_asset_reversal_receipts_operation_id",
                table: "asset_reversal_receipts",
                column: "operation_id",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "asset_reversal_receipts");
        }
    }
}
