using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCfhReportSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<string>(
                    name: "extra_data_id",
                    table: "cfh_reports",
                    type: "varchar(64)",
                    maxLength: 64,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "item_id",
                table: "cfh_reports",
                type: "int",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "cfh_reports",
                type: "int",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "extra_data_id", table: "cfh_reports");

            migrationBuilder.DropColumn(name: "item_id", table: "cfh_reports");

            migrationBuilder.DropColumn(name: "source", table: "cfh_reports");
        }
    }
}
