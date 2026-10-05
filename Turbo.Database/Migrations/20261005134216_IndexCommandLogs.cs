using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class IndexCommandLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_command_logs_command_id",
                table: "command_logs",
                columns: new[] { "command", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_command_logs_player_id_id",
                table: "command_logs",
                columns: new[] { "player_id", "id" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_command_logs_command_id", table: "command_logs");

            migrationBuilder.DropIndex(name: "IX_command_logs_player_id_id", table: "command_logs");
        }
    }
}
