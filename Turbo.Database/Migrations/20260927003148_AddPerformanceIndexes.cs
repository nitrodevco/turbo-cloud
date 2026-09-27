using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reordered by hand: every index created before the one it replaces is dropped.
            // The single-column indexes back foreign keys, and MySQL refuses to drop one until
            // another index starting with the same column exists.
            migrationBuilder.CreateIndex(
                name: "IX_rooms_category_id_score",
                table: "rooms",
                columns: new[] { "category_id", "score" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_rooms_hidden_by_bc",
                table: "rooms",
                column: "hidden_by_bc"
            );

            migrationBuilder.CreateIndex(name: "IX_rooms_score", table: "rooms", column: "score");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_staff_pick_score",
                table: "rooms",
                columns: new[] { "staff_pick", "score" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_entry_logs_player_id_room_id_created_at",
                table: "room_entry_logs",
                columns: new[] { "player_id", "room_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_pets_player_id_room_id",
                table: "pets",
                columns: new[] { "player_id", "room_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ltd_raffle_entries_batch_id_player_id",
                table: "ltd_raffle_entries",
                columns: new[] { "batch_id", "player_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ltd_raffle_entries_series_id_player_id_result",
                table: "ltd_raffle_entries",
                columns: new[] { "series_id", "player_id", "result" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_furniture_player_id_room_id",
                table: "furniture",
                columns: new[] { "player_id", "room_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_bots_player_id_room_id",
                table: "bots",
                columns: new[] { "player_id", "room_id" }
            );

            migrationBuilder.DropIndex(name: "IX_rooms_category_id", table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_room_entry_logs_player_id",
                table: "room_entry_logs"
            );

            migrationBuilder.DropIndex(name: "IX_pets_player_id", table: "pets");

            migrationBuilder.DropIndex(
                name: "IX_ltd_raffle_entries_series_id",
                table: "ltd_raffle_entries"
            );

            migrationBuilder.DropIndex(name: "IX_furniture_player_id", table: "furniture");

            migrationBuilder.DropIndex(name: "IX_bots_player_id", table: "bots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The same order rule as Up: the foreign keys' single-column indexes come back first.
            migrationBuilder.CreateIndex(
                name: "IX_rooms_category_id",
                table: "rooms",
                column: "category_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_entry_logs_player_id",
                table: "room_entry_logs",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_pets_player_id",
                table: "pets",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ltd_raffle_entries_series_id",
                table: "ltd_raffle_entries",
                column: "series_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_furniture_player_id",
                table: "furniture",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_bots_player_id",
                table: "bots",
                column: "player_id"
            );

            migrationBuilder.DropIndex(name: "IX_rooms_category_id_score", table: "rooms");

            migrationBuilder.DropIndex(name: "IX_rooms_hidden_by_bc", table: "rooms");

            migrationBuilder.DropIndex(name: "IX_rooms_score", table: "rooms");

            migrationBuilder.DropIndex(name: "IX_rooms_staff_pick_score", table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_room_entry_logs_player_id_room_id_created_at",
                table: "room_entry_logs"
            );

            migrationBuilder.DropIndex(name: "IX_pets_player_id_room_id", table: "pets");

            migrationBuilder.DropIndex(
                name: "IX_ltd_raffle_entries_batch_id_player_id",
                table: "ltd_raffle_entries"
            );

            migrationBuilder.DropIndex(
                name: "IX_ltd_raffle_entries_series_id_player_id_result",
                table: "ltd_raffle_entries"
            );

            migrationBuilder.DropIndex(name: "IX_furniture_player_id_room_id", table: "furniture");

            migrationBuilder.DropIndex(name: "IX_bots_player_id_room_id", table: "bots");
        }
    }
}
