using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerOnlineTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "online_ms",
                table: "players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L
            );

            migrationBuilder.CreateIndex(
                name: "IX_human_respect_operations_ActorId_Completed",
                table: "human_respect_operations",
                columns: new[] { "ActorId", "Completed" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_human_respect_operations_ActorId_Completed",
                table: "human_respect_operations"
            );

            migrationBuilder.DropColumn(name: "online_ms", table: "players");
        }
    }
}
