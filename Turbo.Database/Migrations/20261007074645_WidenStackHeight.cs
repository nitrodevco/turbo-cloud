using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class WidenStackHeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "stack_height",
                table: "furniture_definitions",
                type: "double(10,4)",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double(10,3)",
                oldDefaultValue: 0.0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "stack_height",
                table: "furniture_definitions",
                type: "double(10,3)",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double(10,4)",
                oldDefaultValue: 0.0
            );
        }
    }
}
