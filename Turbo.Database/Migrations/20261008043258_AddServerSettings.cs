using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServerSettings : Migration
    {
        /// <summary>The variable each gamedata file's address is read from, as Nitro reads it.</summary>
        private static readonly (string Key, string File)[] GAMEDATA_ADDRESSES =
        [
            ("furnituredata.url", "furnidata_json"),
            ("productdata.url", "productdata_json"),
            ("gamedata.urls.externalTexts", "external_flash_texts"),
            ("figuredata.url", "figuredata_json"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<string>(
                    name: "linked_file",
                    table: "gamedata_variables",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AddColumn<string>(
                    name: "setting_path",
                    table: "gamedata_variables",
                    type: "varchar(255)",
                    maxLength: 255,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            // The client's addresses of the hotel's gamedata files follow each file, as the hotel
            // wrote them by hash before variables could follow anything. A variable already there
            // keeps its value, written while the hotel has no public address; one that isn't
            // starts at the file's address that never changes.
            foreach (var (key, file) in GAMEDATA_ADDRESSES)
            {
                migrationBuilder.Sql(
                    $"UPDATE `gamedata_variables` SET `linked_file` = '{file}' WHERE `variable_key` = '{key}';"
                );
                migrationBuilder.Sql(
                    $"INSERT IGNORE INTO `gamedata_variables` (`variable_key`, `value`, `linked_file`) VALUES ('{key}', '\"/gamedata/{file}/0\"', '{file}');"
                );
            }

            migrationBuilder
                .CreateTable(
                    name: "server_setting_changes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        setting_path = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        before = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        after = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        secret = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_server_setting_changes", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "server_settings",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        setting_path = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        value = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: false)
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
                        table.PrimaryKey("PK_server_settings", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_server_setting_changes_setting_path",
                table: "server_setting_changes",
                column: "setting_path"
            );

            migrationBuilder.CreateIndex(
                name: "IX_server_settings_setting_path",
                table: "server_settings",
                column: "setting_path",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "server_setting_changes");

            migrationBuilder.DropTable(name: "server_settings");

            migrationBuilder.DropColumn(name: "linked_file", table: "gamedata_variables");

            migrationBuilder.DropColumn(name: "setting_path", table: "gamedata_variables");
        }
    }
}
