using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigatorPublicCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "staff_picked_at",
                table: "rooms",
                type: "datetime(6)",
                nullable: true
            );

            migrationBuilder
                .CreateTable(
                    name: "navigator_public_categories",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        name = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        order_num = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        force_closed = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        view_mode = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 1
                        ),
                        visible = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: true
                        ),
                        kind = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        plugin_key = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
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
                        table.PrimaryKey("PK_navigator_public_categories", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "navigator_public_rooms",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        category_id = table.Column<int>(type: "int", nullable: false),
                        room_id = table.Column<int>(type: "int", nullable: false),
                        order_num = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        added_by = table.Column<int>(type: "int", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_navigator_public_rooms", x => x.id);
                        table.ForeignKey(
                            name: "FK_navigator_public_rooms_navigator_public_categories_category_~",
                            column: x => x.category_id,
                            principalTable: "navigator_public_categories",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_navigator_public_rooms_rooms_room_id",
                            column: x => x.room_id,
                            principalTable: "rooms",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_navigator_public_categories_plugin_key",
                table: "navigator_public_categories",
                column: "plugin_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_navigator_public_rooms_category_id_room_id",
                table: "navigator_public_rooms",
                columns: new[] { "category_id", "room_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_navigator_public_rooms_room_id",
                table: "navigator_public_rooms",
                column: "room_id"
            );

            // The staff picks as a category of the Public tab, hidden: out of the box the tab
            // looks as Habbo's, which never sent a staffpicks block; staff can show it.
            migrationBuilder.Sql(
                "INSERT INTO `navigator_public_categories` (`name`, `order_num`, `force_closed`, `view_mode`, `visible`, `kind`, `plugin_key`, `created_at`) "
                    + "SELECT '', 0, 0, 1, 0, 1, NULL, CURRENT_TIMESTAMP FROM (SELECT 1) AS `one` "
                    + "WHERE NOT EXISTS (SELECT 1 FROM `navigator_public_categories` WHERE `kind` = 1);"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "navigator_public_rooms");

            migrationBuilder.DropTable(name: "navigator_public_categories");

            migrationBuilder.DropColumn(name: "staff_picked_at", table: "rooms");
        }
    }
}
