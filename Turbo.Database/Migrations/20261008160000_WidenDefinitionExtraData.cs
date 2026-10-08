using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// A definition's extra data holds named JSON sections, and a crackable's rewards or a vending
    /// machine's items run past 512 characters (one reward list is 1763), so
    /// <c>MapFurnitureBehaviours</c> stopped on MySQL with "Data too long for column 'extra_data'".
    /// Ordered just before it, so a hotel stopped there widens the column and then carries on.
    /// </summary>
    public partial class WidenDefinitionExtraData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "extra_data",
                    table: "furniture_definitions",
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
                    table: "furniture_definitions",
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
