using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Splits the catalog into trees. Every page names the catalog it belongs to, and offers and
    /// products inherit it from their page, so a page is the only row that says which tree
    /// anything is in. Existing pages default to the normal catalog.
    /// <para>
    /// A root for the Builders Club tree is seeded, because a catalog with no root cannot be sent
    /// at all and the client would ask for it again on every furni it selects. It is left empty:
    /// which furni a hotel lends and how it arranges them is the operator's decision.
    /// </para>
    /// </summary>
    public partial class AddBuildersClubCatalogType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "catalog_type",
                table: "catalog_pages",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateIndex(
                name: "IX_catalog_pages_catalog_type",
                table: "catalog_pages",
                column: "catalog_type"
            );

            // catalog_type 1 is CatalogType.BuildersClub. The root is the page with no parent,
            // which is how the provider finds it; re-running this adds nothing.
            migrationBuilder.Sql(
                """
                INSERT INTO catalog_pages (catalog_type, parent_id, localization, name, icon, layout, sort_order, visible)
                SELECT 1, NULL, 'builders_club', 'Builders Club', 0, 'default_3x3', 0, 1
                FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM (
                        SELECT id FROM catalog_pages WHERE catalog_type = 1 AND parent_id IS NULL LIMIT 1
                    ) AS present
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_pages_catalog_type",
                table: "catalog_pages"
            );

            migrationBuilder.DropColumn(name: "catalog_type", table: "catalog_pages");
        }
    }
}
