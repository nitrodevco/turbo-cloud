using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class TextKeysCaseSensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "text_key",
                    table: "habbo_texts",
                    type: "varchar(255)",
                    maxLength: 255,
                    nullable: false,
                    collation: "utf8mb4_bin",
                    oldClrType: typeof(string),
                    oldType: "varchar(255)",
                    oldMaxLength: 255
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AlterColumn<string>(
                    name: "text_key",
                    table: "gamedata_texts",
                    type: "varchar(255)",
                    maxLength: 255,
                    nullable: false,
                    collation: "utf8mb4_bin",
                    oldClrType: typeof(string),
                    oldType: "varchar(255)",
                    oldMaxLength: 255
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "text_key",
                    table: "habbo_texts",
                    type: "varchar(255)",
                    maxLength: 255,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "varchar(255)",
                    oldMaxLength: 255,
                    oldCollation: "utf8mb4_bin"
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AlterColumn<string>(
                    name: "text_key",
                    table: "gamedata_texts",
                    type: "varchar(255)",
                    maxLength: 255,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "varchar(255)",
                    oldMaxLength: 255,
                    oldCollation: "utf8mb4_bin"
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
