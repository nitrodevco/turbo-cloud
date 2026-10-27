using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeAudiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "gamedata_themes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        plugin_key = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        is_default = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        selectable = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        audience_players = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        audience_groups = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_gamedata_themes", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_themes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        plugin_key = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
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
                        table.PrimaryKey("PK_player_themes", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_themes_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_gamedata_themes_plugin_key",
                table: "gamedata_themes",
                column: "plugin_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_themes_player_id",
                table: "player_themes",
                column: "player_id",
                unique: true
            );

            // The themes this hotel has now were every player's: the first start keeps each Live and
            // takes this row away (ThemeAudienceService.AdoptAsync). A theme added later starts in development.
            migrationBuilder.Sql(
                "INSERT IGNORE INTO `gamedata_themes` (`plugin_key`, `status`, `is_default`, `selectable`, `audience_players`, `audience_groups`, `updated_at`, `created_at`) VALUES ('*', 0, 0, 0, '[]', '[]', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "gamedata_themes");

            migrationBuilder.DropTable(name: "player_themes");
        }
    }
}
