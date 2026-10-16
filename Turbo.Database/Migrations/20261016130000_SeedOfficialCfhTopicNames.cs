using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives the call for help topics the names and consequences Habbo sends for them. Every
    /// official CfhTopicsInit recorded (6 official client captures, 2026-10-12, all identical)
    /// carries the same 27 topics in the tree SeedOfficialCfhTopicTree seeded, each with its own
    /// name (explicit_sexual_talk, cybersex, ...) and the consequence auto_reply, except 21
    /// (self_threatening), whose consequence is self_harm_notification. "Sex links" is topic 36
    /// there, not 30 (help.cfh.topic.30 and .36 are the same text, so the window looked the same).
    /// The Flash client reads the consequence into CallForHelpTopicData and nothing more; the ids
    /// the client shows a topic by (help.cfh.topic.&lt;id&gt;) do not change.
    /// Only a row still exactly as the earlier seeds left it is changed, so a topic a hotel edited
    /// keeps what it chose.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261016130000_SeedOfficialCfhTopicNames")]
    public partial class SeedOfficialCfhTopicNames : Migration
    {
        // (id, category, name as seeded, sort order as seeded, Habbo's name, Habbo's consequence)
        private static readonly (
            int Id,
            string Category,
            string SeededName,
            int SortOrder,
            string Name,
            string Consequence
        )[] TOPICS =
        [
            (1, "sexual_content", "topic_1", 100, "explicit_sexual_talk", "auto_reply"),
            (2, "sexual_content", "topic_2", 101, "cybersex", "auto_reply"),
            (3, "sexual_content", "topic_3", 102, "sexual_webcam_images", "auto_reply"),
            (
                39,
                "sexual_content",
                "topic_39",
                104,
                "sexual_abuse_and_exploitation_of_children_anonymous_report",
                "auto_reply"
            ),
            (6, "pii_meeting_irl", "topic_6", 200, "meet_irl", "auto_reply"),
            (8, "pii_meeting_irl", "topic_8", 201, "asking_pii", "auto_reply"),
            (9, "scamming", "topic_9", 300, "scamsites_promoting", "auto_reply"),
            (10, "scamming", "topic_10", 301, "selling_buying_accounts_or_furni", "auto_reply"),
            (11, "scamming", "topic_11", 302, "stealing_accounts_or_furni", "auto_reply"),
            (32, "scamming", "topic_32", 303, "hacking_scamming_tricks", "auto_reply"),
            (33, "scamming", "topic_33", 304, "fraud", "auto_reply"),
            (12, "trolling_bad_behavior", "bullying", 400, "bullying", "auto_reply"),
            (13, "trolling_bad_behavior", "habbo_name", 401, "habbo_name", "auto_reply"),
            (
                34,
                "trolling_bad_behavior",
                "inappropiate_room_group_event",
                402,
                "inappropiate_room_group_event",
                "auto_reply"
            ),
            (14, "trolling_bad_behavior", "topic_14", 403, "swearing", "auto_reply"),
            (15, "trolling_bad_behavior", "topic_15", 404, "drugs_promotion", "auto_reply"),
            (16, "trolling_bad_behavior", "topic_16", 405, "gambling", "auto_reply"),
            (17, "trolling_bad_behavior", "topic_17", 406, "staff_impersonation", "auto_reply"),
            (18, "trolling_bad_behavior", "topic_18", 407, "minors_access", "auto_reply"),
            (19, "violent_behavior", "topic_19", 500, "hate_speech", "auto_reply"),
            (20, "violent_behavior", "topic_20", 501, "violent_roleplay", "auto_reply"),
            (21, "violent_behavior", "topic_21", 502, "self_threatening", "self_harm_notification"),
            (22, "game_interruption", "topic_22", 600, "flooding", "auto_reply"),
            (29, "game_interruption", "topic_29", 601, "raids", "auto_reply"),
            (35, "game_interruption", "topic_35", 602, "scripting", "auto_reply"),
            (40, "unlawful_activity", "topic_40", 700, "unlawful_activity", "auto_reply"),
        ];

        // Sex links moves from 30 to Habbo's 36, in the same place, if 30 is still as seeded.
        private const string MOVE_SEX_LINKS =
            "INSERT IGNORE INTO cfh_topics (id, category, name, consequence, sort_order, enabled) "
            + "SELECT 36, 'sexual_content', 'sex_links', 'auto_reply', 103, 1 FROM cfh_topics "
            + "WHERE id = 30 AND category = 'sexual_content' AND name = 'topic_30' "
            + "AND consequence = '' AND sort_order = 103 AND enabled = 1";

        private const string REMOVE_SEEDED_30 =
            "DELETE FROM cfh_topics WHERE id = 30 AND category = 'sexual_content' "
            + "AND name = 'topic_30' AND consequence = '' AND sort_order = 103 AND enabled = 1 "
            + "AND EXISTS (SELECT 1 FROM (SELECT id FROM cfh_topics WHERE id = 36) AS moved)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var topic in TOPICS)
            {
                migrationBuilder.Sql(
                    $"UPDATE cfh_topics SET name = '{topic.Name}', consequence = '{topic.Consequence}' "
                        + $"WHERE id = {topic.Id} AND category = '{topic.Category}' "
                        + $"AND name = '{topic.SeededName}' AND consequence = '' "
                        + $"AND sort_order = {topic.SortOrder} AND enabled = 1"
                );
            }

            migrationBuilder.Sql(MOVE_SEX_LINKS);
            migrationBuilder.Sql(REMOVE_SEEDED_30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A seeded row is indistinguishable from an operator's once edited; it stays.
        }
    }
}
