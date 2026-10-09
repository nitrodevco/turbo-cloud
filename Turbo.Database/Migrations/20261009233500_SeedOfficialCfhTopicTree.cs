using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// The call for help topics as Habbo's report window showed them on 2026-10-09 (official
    /// capture, a populated public room, nothing sent; INDEX.md "Official CFH topic tree"): seven
    /// categories and 27 topics, in this order, each display name being the hotel's
    /// help.cfh.topic.&lt;id&gt; text. Against SeedEvidencedCfhTopics: 39 ("Sexual Abuse and
    /// Exploitation of Children (Anonymous Report)") is the last of Sexual content, not Unlawful
    /// activity; 34 ("Inappropriate room/group/event") comes third in Trolling and bad behavior;
    /// 31 ("Sexually inappropriate behaviour") and 23 ("Blocking doors") are not offered. "Sex links"
    /// stays 30 (36 has the same text). Only the rows still exactly as SeedEvidencedCfhTopics seeded
    /// them are replaced, so a topic a hotel changed keeps what it chose.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261009233500_SeedOfficialCfhTopicTree")]
    public partial class SeedOfficialCfhTopicTree : Migration
    {
        private const string REMOVE_AS_SEEDED =
            "DELETE FROM cfh_topics WHERE (id, category, name, consequence, sort_order, enabled) IN ("
            + "(31, 'sexual_content', 'topic_31', '', 104, 1), "
            + "(14, 'trolling_bad_behavior', 'topic_14', '', 402, 1), "
            + "(15, 'trolling_bad_behavior', 'topic_15', '', 403, 1), "
            + "(16, 'trolling_bad_behavior', 'topic_16', '', 404, 1), "
            + "(17, 'trolling_bad_behavior', 'topic_17', '', 405, 1), "
            + "(18, 'trolling_bad_behavior', 'topic_18', '', 406, 1), "
            + "(34, 'trolling_bad_behavior', 'inappropiate_room_group_event', '', 407, 1), "
            + "(23, 'game_interruption', 'topic_23', '', 601, 1), "
            + "(29, 'game_interruption', 'topic_29', '', 602, 1), "
            + "(35, 'game_interruption', 'topic_35', '', 603, 1), "
            + "(39, 'unlawful_activity', 'topic_39', '', 700, 1), "
            + "(40, 'unlawful_activity', 'topic_40', '', 701, 1));";

        private const string SEED =
            "INSERT IGNORE INTO cfh_topics (id, category, name, consequence, sort_order, enabled) VALUES "
            + "(39, 'sexual_content', 'topic_39', '', 104, 1), "
            + "(34, 'trolling_bad_behavior', 'inappropiate_room_group_event', '', 402, 1), "
            + "(14, 'trolling_bad_behavior', 'topic_14', '', 403, 1), "
            + "(15, 'trolling_bad_behavior', 'topic_15', '', 404, 1), "
            + "(16, 'trolling_bad_behavior', 'topic_16', '', 405, 1), "
            + "(17, 'trolling_bad_behavior', 'topic_17', '', 406, 1), "
            + "(18, 'trolling_bad_behavior', 'topic_18', '', 407, 1), "
            + "(29, 'game_interruption', 'topic_29', '', 601, 1), "
            + "(35, 'game_interruption', 'topic_35', '', 602, 1), "
            + "(40, 'unlawful_activity', 'topic_40', '', 700, 1);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(REMOVE_AS_SEEDED);
            migrationBuilder.Sql(SEED);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A seeded row is indistinguishable from an operator's once edited; it stays.
        }
    }
}
