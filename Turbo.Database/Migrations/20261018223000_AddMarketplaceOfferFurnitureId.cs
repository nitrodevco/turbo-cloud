using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// A marketplace offer keeps the id its item's furniture row had when it was listed
    /// (<c>marketplace_offers.furniture_id</c>; the row is deleted, so no foreign key): the
    /// movement that takes the item off the offer, sold or taken back, names the asset its listing
    /// put there. Offers listed before stay null.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018223000_AddMarketplaceOfferFurnitureId")]
    public partial class AddMarketplaceOfferFurnitureId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "furniture_id",
                table: "marketplace_offers",
                type: "int",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "furniture_id", table: "marketplace_offers");
        }
    }
}
