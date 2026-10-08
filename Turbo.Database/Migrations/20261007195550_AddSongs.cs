using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSongs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "held_position",
                table: "furniture",
                type: "int",
                nullable: true
            );

            migrationBuilder
                .CreateTable(
                    name: "songs",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        name = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        author = table
                            .Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        track = table
                            .Column<string>(type: "longtext", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        length_seconds = table.Column<int>(type: "int", nullable: false),
                        is_official = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_songs", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_songs_code",
                table: "songs",
                column: "code",
                unique: true
            );

            // The stock jukeboxes and trax machines, which stood as plain furni.
            foreach (var (logic, pattern) in STOCK_LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{logic}' WHERE `logic` = 'default_floor' AND `name` LIKE '{pattern}';"
                );

            // Every trax song furni is a song disk, whatever it is called (category 8, TraxSong).
            migrationBuilder.Sql(
                "UPDATE `furniture_definitions` SET `logic` = 'song_disk' WHERE `logic` = 'default_floor' AND `category` = 8;"
            );
        }

        private static readonly (string Logic, string Pattern)[] STOCK_LOGICS =
        [
            ("jukebox", "%jukebox%"),
            ("sound_machine", "sound_machine%"),
            ("sound_machine", "nouvelle_trax"),
            ("sound_machine", "ads_idol_trax"),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var logic in STOCK_LOGICS.Select(x => x.Logic).Append("song_disk").Distinct())
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE `logic` = '{logic}';"
                );

            migrationBuilder.DropTable(name: "songs");

            migrationBuilder.DropColumn(name: "held_position", table: "furniture");
        }
    }
}
