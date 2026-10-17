using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddClientMaps : Migration
    {
        /// <summary>The variable each map's address is read from, as Nitro reads it.</summary>
        private static readonly (string Key, string File)[] MAP_ADDRESSES =
        [
            ("figuremap.url", "figuremap_json"),
            ("effectmap.url", "effectmap_json"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<string>(
                    name: "map_entries",
                    table: "asset_bundles",
                    type: "longtext",
                    maxLength: 1048576,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            // The client's figure and effect maps are the hotel's own, built from its bundles: their
            // addresses follow the files, as the other gamedata files' do. A variable already there
            // follows it from now on; one that isn't starts at the file's address that never changes.
            foreach (var (key, file) in MAP_ADDRESSES)
            {
                migrationBuilder.Sql(
                    $"UPDATE `gamedata_variables` SET `linked_file` = '{file}', `setting_path` = NULL WHERE `variable_key` = '{key}';"
                );
                migrationBuilder.Sql(
                    $"INSERT IGNORE INTO `gamedata_variables` (`variable_key`, `value`, `linked_file`) VALUES ('{key}', '\"/gamedata/{file}/0\"', '{file}');"
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (key, file) in MAP_ADDRESSES)
                migrationBuilder.Sql(
                    $"UPDATE `gamedata_variables` SET `linked_file` = NULL WHERE `variable_key` = '{key}' AND `linked_file` = '{file}';"
                );

            migrationBuilder.DropColumn(name: "map_entries", table: "asset_bundles");
        }
    }
}
