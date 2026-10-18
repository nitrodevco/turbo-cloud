using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class GamedataChangesByRecordType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Made before the old one goes: MySQL keeps an index under the change set's foreign key.
            migrationBuilder.CreateIndex(
                name: "IX_gamedata_changes_change_set_id_record_type",
                table: "gamedata_changes",
                columns: new[] { "change_set_id", "record_type" }
            );

            migrationBuilder.DropIndex(
                name: "IX_gamedata_changes_change_set_id",
                table: "gamedata_changes"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_gamedata_changes_change_set_id",
                table: "gamedata_changes",
                column: "change_set_id"
            );

            migrationBuilder.DropIndex(
                name: "IX_gamedata_changes_change_set_id_record_type",
                table: "gamedata_changes"
            );
        }
    }
}
