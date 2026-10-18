using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// The asset movement outbox (<c>asset_movement_outbox</c>): one row per furni, pet, bot or
    /// currency changing owner, place or existence, written with the change while
    /// <c>Turbo:Movements:Enabled</c> is on (off by default, so the table stays empty). No foreign
    /// keys and no unique key: it outlives what it names, and a retried save writes its rows again.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018160000_AddAssetMovementOutbox")]
    public partial class AddAssetMovementOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "asset_movement_outbox",
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
                        seq = table.Column<short>(type: "smallint", nullable: false),
                        occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        asset_kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                        asset_id = table.Column<long>(type: "bigint", nullable: false),
                        definition_id = table.Column<int>(type: "int", nullable: false),
                        currency_type = table.Column<int>(type: "int", nullable: true),
                        activity_point_type = table.Column<int>(type: "int", nullable: true),
                        from_owner = table.Column<int>(type: "int", nullable: false),
                        from_kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                        from_place = table.Column<long>(type: "bigint", nullable: false),
                        to_owner = table.Column<int>(type: "int", nullable: false),
                        to_kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                        to_place = table.Column<long>(type: "bigint", nullable: false),
                        x = table.Column<int>(type: "int", nullable: true),
                        y = table.Column<int>(type: "int", nullable: true),
                        z = table.Column<double>(type: "double", nullable: true),
                        rot = table.Column<int>(type: "int", nullable: true),
                        wall_offset = table.Column<int>(type: "int", nullable: true),
                        amount = table.Column<long>(type: "bigint", nullable: false),
                        actor = table.Column<int>(type: "int", nullable: false),
                        cause = table
                            .Column<string>(type: "varchar(48)", maxLength: 48, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        cause_ref = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        snapshot_extra_data = table
                            .Column<string>(type: "longtext", nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_movement_outbox", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_asset_movement_outbox_created_at",
                table: "asset_movement_outbox",
                column: "created_at"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "asset_movement_outbox");
        }
    }
}
