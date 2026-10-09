using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// The three campaigns Habbo's quest window showed on 2026-10-09 (quests.png), in its order:
    /// A True Gamer! (bawcollab_worlds, 10 quests, seasonal, "5 mon." left), A True True Gamer!
    /// (bawcollab_dlc, 3, seasonal) and Connect the Dots! (connect_dots26, 7, "Reward: 50" in
    /// duckets, activity point type 0). Quest codes, chains and order are the hotel's
    /// quests.&lt;campaign&gt;.&lt;code&gt; texts. Inference, marked: the end date (five months
    /// after the capture), the badges worn (badge_name_W26xx for the worlds; the DLC pass badges
    /// W26V1-3 for the DLC), the types (WEAR_BADGE; the dot quests' CONNECT_DOTS_* and the
    /// one-step scores have no counting source yet) and the dot quests' 50 steps and rewards.
    /// INSERT IGNORE, so a hotel that changed them keeps what it chose.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261009233400_SeedQuestCampaigns")]
    public partial class SeedQuestCampaigns : Migration
    {
        private const string CAMPAIGNS =
            "INSERT IGNORE INTO quest_campaigns (code, sort_order, enabled, ends_at) VALUES "
            + "('bawcollab_worlds', 1, 1, '2027-03-09 00:00:00'), "
            + "('bawcollab_dlc', 2, 1, '2027-03-09 00:00:00'), "
            + "('connect_dots26', 3, 1, NULL);";

        private static readonly (
            string Campaign,
            string Code,
            string Type,
            string Target,
            int Steps,
            int Reward,
            string Chain
        )[] QUESTS =
        [
            ("bawcollab_worlds", "1771579447385", "WEAR_BADGE", "W2601", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771579590928", "WEAR_BADGE", "W2610", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771579734993", "WEAR_BADGE", "W2609", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771579820366", "WEAR_BADGE", "W2604", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771579914271", "WEAR_BADGE", "W2603", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771580011052", "WEAR_BADGE", "W2602", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771580115098", "WEAR_BADGE", "W2605", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771580187651", "WEAR_BADGE", "W2606", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771580591567", "WEAR_BADGE", "W2608", 1, 0, "1771579236591"),
            ("bawcollab_worlds", "1771580655461", "WEAR_BADGE", "W2607", 1, 0, "1771579236591"),
            ("bawcollab_dlc", "1771583870994", "WEAR_BADGE", "W26V2", 1, 0, "1771583727360"),
            ("bawcollab_dlc", "1771584000727", "WEAR_BADGE", "W26V3", 1, 0, "1771583727360"),
            ("bawcollab_dlc", "1771584285810", "WEAR_BADGE", "W26V1", 1, 0, "1771583727360"),
            ("connect_dots26", "1775040549468", "CONNECT_DOTS_RED", "", 50, 50, ""),
            ("connect_dots26", "1775047542145", "CONNECT_DOTS_YELLOW", "", 50, 50, ""),
            ("connect_dots26", "1775047630826", "CONNECT_DOTS_BLUE", "", 50, 50, ""),
            ("connect_dots26", "1775047706464", "CONNECT_DOTS_GREEN", "", 50, 50, ""),
            ("connect_dots26", "1775047779417", "CONNECT_DOTS_SCORE_MOVE", "", 1, 50, ""),
            ("connect_dots26", "1775047832397", "CONNECT_DOTS_ALL_MOVES", "", 1, 50, ""),
            ("connect_dots26", "1775052215082", "CONNECT_DOTS_SCORE_MINUTE", "", 1, 50, ""),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CAMPAIGNS);

            for (var i = 0; i < QUESTS.Length; i++)
            {
                var (campaign, code, type, target, steps, reward, chain) = QUESTS[i];

                migrationBuilder.Sql(
                    "INSERT IGNORE INTO quests (campaign_id, localization_code, type, target, "
                        + "total_steps, activity_point_type, reward_amount, sort_order, "
                        + "image_version, catalog_page_name, chain_code, easy) "
                        + $"SELECT id, '{code}', '{type}', '{target}', {steps}, 0, {reward}, {i}, "
                        + $"'', '', '{chain}', 0 FROM quest_campaigns WHERE code = '{campaign}';"
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A seeded row is indistinguishable from an operator's once edited; it stays.
        }
    }
}
