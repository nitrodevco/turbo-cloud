using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomWiredSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "wired_modify_permission_mask",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 2
            );

            migrationBuilder.AddColumn<int>(
                name: "wired_read_permission_mask",
                table: "rooms",
                type: "int",
                nullable: false,
                defaultValue: 2
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "wired_timezone",
                    table: "rooms",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: false,
                    defaultValue: "UTC"
                )
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "wired_modify_permission_mask", table: "rooms");

            migrationBuilder.DropColumn(name: "wired_read_permission_mask", table: "rooms");

            migrationBuilder.DropColumn(name: "wired_timezone", table: "rooms");
        }
    }
}
