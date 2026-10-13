using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class NavigatorSearchCodesCaseSensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "search_code",
                    table: "player_navigator_view_modes",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: false,
                    collation: "utf8mb4_bin",
                    oldClrType: typeof(string),
                    oldType: "varchar(64)",
                    oldMaxLength: 64
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AlterColumn<string>(
                    name: "search_code",
                    table: "player_navigator_collapsed_categories",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: false,
                    collation: "utf8mb4_bin",
                    oldClrType: typeof(string),
                    oldType: "varchar(64)",
                    oldMaxLength: 64
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "search_code",
                    table: "player_navigator_view_modes",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "varchar(64)",
                    oldMaxLength: 64,
                    oldCollation: "utf8mb4_bin"
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AlterColumn<string>(
                    name: "search_code",
                    table: "player_navigator_collapsed_categories",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "varchar(64)",
                    oldMaxLength: 64,
                    oldCollation: "utf8mb4_bin"
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
