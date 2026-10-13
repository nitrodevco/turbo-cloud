using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class FurnitureExtraDataLongText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "extra_data",
                    table: "furniture",
                    type: "longtext",
                    maxLength: 512,
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "varchar(512)",
                    oldMaxLength: 512,
                    oldNullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AlterColumn<string>(
                    name: "extra_data",
                    table: "builders_club_furniture",
                    type: "longtext",
                    maxLength: 512,
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "varchar(512)",
                    oldMaxLength: 512,
                    oldNullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "extra_data",
                    table: "furniture",
                    type: "varchar(512)",
                    maxLength: 512,
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "longtext",
                    oldMaxLength: 512,
                    oldNullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AlterColumn<string>(
                    name: "extra_data",
                    table: "builders_club_furniture",
                    type: "varchar(512)",
                    maxLength: 512,
                    nullable: true,
                    oldClrType: typeof(string),
                    oldType: "longtext",
                    oldMaxLength: 512,
                    oldNullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
