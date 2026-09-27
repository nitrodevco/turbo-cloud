using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddChatStylePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ambassador_only",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "club_only",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "name",
                    table: "player_chat_styles",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "purchasable",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "staff_only",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "system",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ambassador_only", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "club_only", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "name", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "purchasable", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "staff_only", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "system", table: "player_chat_styles");
        }
    }
}
