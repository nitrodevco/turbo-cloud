using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Seeds the Habbo Club memberships the club centre sells, so a fresh hotel has something to
    /// buy the moment the subscription system is up. Three offers of one, three and twelve
    /// periods, each with a product that grants <c>SubscriptionType.HabboClub</c> days; the
    /// longest is what <c>ICatalogService.GetClubExtendOffer</c> offers as a renewal, priced
    /// against the shortest, so the saving the client draws is real.
    /// <para>
    /// They hang off a hidden page, because <c>GetClubOffers</c> finds them by their product and
    /// not by where they sit, and because which page a hotel wants its memberships drawn on — and
    /// with which layout and artwork — is the operator's decision, not this migration's. Make the
    /// page visible and give it a layout to turn it into a real catalog page.
    /// </para>
    /// Every statement is guarded, so a hotel that already sells Habbo Club is left alone and
    /// re-running this changes nothing.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260920080000_SeedHabboClubOffers")]
    public partial class SeedHabboClubOffers : Migration
    {
        private const string PAGE_LOCALIZATION = "club_buy";

        /// <summary>Localization id, credits, and days. 31 days is one period.</summary>
        private static readonly (string Code, int Credits, int Days)[] OFFERS =
        [
            ("habbo_club_1_month", 25, 31),
            ("habbo_club_3_months", 60, 93),
            ("habbo_club_12_months", 200, 372),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The root of the normal catalog is its page with no parent, which is how the
            // catalog provider finds it too; catalog_type 0 is CatalogType.Normal, and naming it
            // keeps this off the Builders Club root. A hotel with no catalog at all gets no rows
            // and no error.
            migrationBuilder.Sql(
                $"""
                INSERT INTO catalog_pages (catalog_type, parent_id, localization, name, icon, layout, sort_order, visible)
                SELECT 0, root.id, '{PAGE_LOCALIZATION}', 'Habbo Club', 0, 'default_3x3', 1000, 0
                FROM (SELECT id FROM catalog_pages WHERE parent_id IS NULL AND catalog_type = 0 ORDER BY id LIMIT 1) AS root
                WHERE NOT EXISTS (
                    SELECT 1 FROM (
                        SELECT id FROM catalog_pages WHERE localization = '{PAGE_LOCALIZATION}' LIMIT 1
                    ) AS present
                );
                """
            );

            foreach (var (code, credits, days) in OFFERS)
            {
                // Gifting a membership has no path on the server yet, so the client is not shown
                // the button; bundling a subscription means nothing either.
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO catalog_offers (page_id, localization_id, cost_credits, cost_currency, currency_type_id, can_gift, can_bundle, club_level, visible)
                    SELECT page.id, '{code}', {credits}, 0, NULL, 0, 0, 0, 1
                    FROM catalog_pages page
                    WHERE page.localization = '{PAGE_LOCALIZATION}'
                      AND NOT EXISTS (
                          SELECT 1 FROM (
                              SELECT id FROM catalog_offers WHERE localization_id = '{code}' LIMIT 1
                          ) AS present
                      )
                    LIMIT 1;
                    """
                );

                // product_type 5 is the legacy 'h' the client is sent; subscription_type 0 is
                // SubscriptionType.HabboClub, which is what the purchase actually acts on.
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO catalog_products (offer_id, product_type, definition_id, extra_param, quantity, subscription_type, subscription_days)
                    SELECT offer.id, 5, NULL, NULL, 1, 0, {days}
                    FROM catalog_offers offer
                    WHERE offer.localization_id = '{code}'
                      AND NOT EXISTS (
                          SELECT 1 FROM (
                              SELECT p.id FROM catalog_products p
                              JOIN catalog_offers o ON o.id = p.offer_id
                              WHERE o.localization_id = '{code}' AND p.subscription_type = 0
                              LIMIT 1
                          ) AS present
                      )
                    LIMIT 1;
                    """
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded catalog rows are indistinguishable from operator rows once priced or moved,
            // and dropping them would take a hotel's memberships with them; they stay.
        }
    }
}
