using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropBuildersClubFurnitureOfferId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "offer_id", table: "builders_club_furniture");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "offer_id",
                table: "builders_club_furniture",
                type: "int",
                nullable: false,
                defaultValue: 0
            );
        }
    }
}
