using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Puts both catalogs back in one tree of pages. A page's <c>display</c> says which catalogs
    /// show it (0 regular, 1 Builders Club only, 2 both, 3 invisible), in place of the tree it
    /// was in and whether it was shown. A hidden page becomes invisible and a Builders Club page
    /// Builders Club only.
    /// <para>
    /// The Builders Club tree had a root of its own. Left empty, as it was seeded, it is
    /// deleted; with pages under it, it becomes a Builders Club only tab of the one tree, which
    /// that catalog does not draw, so its pages are shown there.
    /// </para>
    /// </summary>
    public partial class ReplaceCatalogTypeWithPageDisplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "display",
                table: "catalog_pages",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.Sql(
                """
                UPDATE catalog_pages
                SET `display` = CASE
                    WHEN `visible` = 0 THEN 3
                    WHEN `catalog_type` = 1 THEN 1
                    ELSE 0
                END;
                """
            );

            // MySQL reads the table it changes only through a derived table.
            migrationBuilder.Sql(
                """
                DELETE FROM catalog_pages
                WHERE `id` IN (
                    SELECT `id` FROM (
                        SELECT p.`id`
                        FROM catalog_pages p
                        WHERE p.`catalog_type` = 1
                            AND p.`parent_id` IS NULL
                            AND NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.`parent_id` = p.`id`)
                            AND NOT EXISTS (SELECT 1 FROM catalog_offers o WHERE o.`page_id` = p.`id`)
                    ) AS empty_roots
                );
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE catalog_pages
                SET `parent_id` = (
                    SELECT `id` FROM (
                        SELECT MIN(`id`) AS `id`
                        FROM catalog_pages
                        WHERE `catalog_type` = 0 AND `parent_id` IS NULL
                    ) AS normal_root
                )
                WHERE `catalog_type` = 1 AND `parent_id` IS NULL;
                """
            );

            migrationBuilder.DropIndex(
                name: "IX_catalog_pages_catalog_type",
                table: "catalog_pages"
            );

            migrationBuilder.DropColumn(name: "catalog_type", table: "catalog_pages");

            migrationBuilder.DropColumn(name: "visible", table: "catalog_pages");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Every page goes back into the normal tree, as the Builders Club one had no pages of
        /// its own to return to; a Builders Club only page is marked as that catalog's.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "catalog_type",
                table: "catalog_pages",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<bool>(
                name: "visible",
                table: "catalog_pages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.Sql(
                """
                UPDATE catalog_pages
                SET `visible` = `display` <> 3,
                    `catalog_type` = CASE WHEN `display` = 1 THEN 1 ELSE 0 END;
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_catalog_pages_catalog_type",
                table: "catalog_pages",
                column: "catalog_type"
            );

            migrationBuilder.DropColumn(name: "display", table: "catalog_pages");
        }
    }
}
