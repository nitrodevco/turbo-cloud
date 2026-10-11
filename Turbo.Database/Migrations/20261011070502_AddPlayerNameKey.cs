using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerNameKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_players_name", table: "players");

            migrationBuilder
                .AddColumn<string>(
                    name: "name_key",
                    table: "players",
                    type: "varchar(512)",
                    maxLength: 512,
                    nullable: false,
                    defaultValue: ""
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            // PlayerNames.Key for the players there are: names are ASCII, so LOWER(TRIM()) is the
            // same key the code computes. Before the index, which every row must satisfy.
            migrationBuilder.Sql("UPDATE players SET name_key = LOWER(TRIM(name));");

            migrationBuilder.CreateIndex(
                name: "IX_players_name_key",
                table: "players",
                column: "name_key",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_players_name_key", table: "players");

            migrationBuilder.DropColumn(name: "name_key", table: "players");

            migrationBuilder.CreateIndex(
                name: "IX_players_name",
                table: "players",
                column: "name",
                unique: true
            );
        }
    }
}
