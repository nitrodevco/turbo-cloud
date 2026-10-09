using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddForumCallsForHelp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "forum_message_id",
                table: "cfh_reports",
                type: "int",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "forum_thread_id",
                table: "cfh_reports",
                type: "int",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "guild_id",
                table: "cfh_reports",
                type: "int",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "forum_message_id", table: "cfh_reports");

            migrationBuilder.DropColumn(name: "forum_thread_id", table: "cfh_reports");

            migrationBuilder.DropColumn(name: "guild_id", table: "cfh_reports");
        }
    }
}
