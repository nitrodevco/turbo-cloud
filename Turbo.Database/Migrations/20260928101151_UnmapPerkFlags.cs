using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Takes <c>players.perk_flags</c> out of the model without dropping it. Perks are projected
    /// from permissions now (<c>docs/permissions.md</c> §8), <c>SeedPermissions</c> carried every
    /// flag that meant anything into player nodes, and nothing reads the column any more. It stays
    /// in the table because CMS and housekeeping panels write the players table directly, and one
    /// that still inserts <c>perk_flags</c> would fail against a table without it; its default
    /// keeps rows written without it valid. Drop it in a later migration once nothing writes it.
    /// </summary>
    public partial class UnmapPerkFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The column is deliberately left in place; see the class comment.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing was dropped, so there is nothing to put back.
        }
    }
}
