using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "vouchers",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        credits = table.Column<int>(type: "int", nullable: false),
                        currency_type_id = table.Column<int>(type: "int", nullable: true),
                        currency_amount = table.Column<int>(type: "int", nullable: false),
                        furniture_definition_id = table.Column<int>(type: "int", nullable: true),
                        furniture_quantity = table.Column<int>(type: "int", nullable: false),
                        badge_code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        max_uses = table.Column<int>(type: "int", nullable: true),
                        uses = table.Column<int>(type: "int", nullable: false),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        note = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_vouchers", x => x.id);
                        table.ForeignKey(
                            name: "FK_vouchers_currency_types_currency_type_id",
                            column: x => x.currency_type_id,
                            principalTable: "currency_types",
                            principalColumn: "id",
                            onDelete: ReferentialAction.SetNull
                        );
                        table.ForeignKey(
                            name: "FK_vouchers_furniture_definitions_furniture_definition_id",
                            column: x => x.furniture_definition_id,
                            principalTable: "furniture_definitions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.SetNull
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "voucher_redemptions",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        voucher_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_voucher_redemptions", x => x.id);
                        table.ForeignKey(
                            name: "FK_voucher_redemptions_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_voucher_redemptions_vouchers_voucher_id",
                            column: x => x.voucher_id,
                            principalTable: "vouchers",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_player_id",
                table: "voucher_redemptions",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_voucher_redemptions_voucher_id_player_id",
                table: "voucher_redemptions",
                columns: new[] { "voucher_id", "player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_code",
                table: "vouchers",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_currency_type_id",
                table: "vouchers",
                column: "currency_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_furniture_definition_id",
                table: "vouchers",
                column: "furniture_definition_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "voucher_redemptions");

            migrationBuilder.DropTable(name: "vouchers");
        }
    }
}
