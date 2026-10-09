using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Replaces the call for help topics AddCfhTopics and SeedUnlawfulActivityTopics seeded with a
    /// set that rests on Habbo's own client and texts only. What each part rests on:
    /// <list type="bullet">
    /// <item>Categories: the seven help.cfh.reason.&lt;name&gt; texts (sexual_content,
    /// pii_meeting_irl, scamming, trolling_bad_behavior, violent_behavior, game_interruption,
    /// unlawful_activity); unlawful_activity is also the category TopicsFlowHelpController
    /// (_unlawfulCategories) asks a name and an email for.</item>
    /// <item>Topic ids: the help.cfh.topic.&lt;id&gt; texts, which the client shows for a topic
    /// (TopicsFlowHelpController.populateTopics, ModActionCtrl.initializeTopicDropdown).</item>
    /// <item>Three names the client looks a topic up by (TopicsFlowHelpController.getTopic, the
    /// same in JS build 87): habbo_name (openReportingUserName; topic 13, "Inappropriate Habbo
    /// name"), bullying (the guardian hand-off; topic 12, "Bullying") and
    /// inappropiate_room_group_event (populateRoomReportButton, which captions it with topic
    /// 34's text).</item>
    /// </list>
    /// Not shown anywhere, so not seeded as fact: the other topics' names (they are
    /// <c>topic_&lt;id&gt;</c>: the client only needs them unique, because a click finds its
    /// topic by name), the consequence (no client reads it; empty), and the category of each
    /// topic and their order, which follow the meaning of the texts. Left out: 4, 5 and 36 (the
    /// same texts as 3, 2 and 30), 24 (the room report uses 34), 25-28, 37 and 41 (help, account,
    /// useless, auto-triggered, erasure and safety ban are not something a reporter picks) and
    /// 101-106, which neither client build reads.
    /// The earlier rows are removed only where they are still exactly as seeded, and the new ones
    /// are INSERT IGNORE, so a topic a hotel changed keeps what it chose.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261009200000_SeedEvidencedCfhTopics")]
    public partial class SeedEvidencedCfhTopics : Migration
    {
        private const string REMOVE_EARLIER_SEED =
            "DELETE FROM cfh_topics WHERE (id, category, name, consequence, sort_order, enabled) IN ("
            + "(1, 'sexual_content', 'explicit_sexual_talk', 'mods', 100, 1), "
            + "(2, 'sexual_content', 'cybersex', 'mods', 101, 1), "
            + "(3, 'sexual_content', 'sexual_webcam_images', 'mods', 102, 1), "
            + "(31, 'sexual_content', 'sexually_inappropiate_behaviour', 'mods', 103, 1), "
            + "(36, 'sexual_content', 'sex_links', 'mods', 104, 1), "
            + "(6, 'pii_meeting_irl', 'meet_irl', 'mods', 200, 1), "
            + "(8, 'pii_meeting_irl', 'asking_pii', 'mods', 201, 1), "
            + "(9, 'scamming', 'scamsites_promoting', 'mods', 300, 1), "
            + "(10, 'scamming', 'selling_buying_accounts_or_furni', 'mods', 301, 1), "
            + "(11, 'scamming', 'stealing_accounts_or_furni', 'mods', 302, 1), "
            + "(32, 'scamming', 'hacking_scamming_tricks', 'auto_reply', 303, 1), "
            + "(33, 'scamming', 'fraud', 'auto_reply', 304, 1), "
            + "(12, 'trolling_bad_behavior', 'bullying', 'mods_till_logout', 400, 1), "
            + "(13, 'trolling_bad_behavior', 'habbo_name', 'mods', 401, 1), "
            + "(14, 'trolling_bad_behavior', 'swearing', 'auto_reply', 402, 1), "
            + "(15, 'trolling_bad_behavior', 'drugs_promotion', 'mods_till_logout', 403, 1), "
            + "(16, 'trolling_bad_behavior', 'gambling', 'auto_reply', 404, 1), "
            + "(17, 'trolling_bad_behavior', 'staff_impersonation', 'mods', 405, 1), "
            + "(18, 'trolling_bad_behavior', 'minors_access', 'auto_reply', 406, 1), "
            + "(34, 'trolling_bad_behavior', 'inappropiate_room_group_event', 'mods', 407, 1), "
            + "(19, 'violent_behavior', 'hate_speech', 'mods_till_logout', 500, 1), "
            + "(20, 'violent_behavior', 'violent_roleplay', 'mods_till_logout', 501, 1), "
            + "(21, 'violent_behavior', 'self_threatening', 'mods', 502, 1), "
            + "(22, 'game_interruption', 'flooding', 'mods_till_logout', 600, 1), "
            + "(23, 'game_interruption', 'door_blocking', 'auto_reply', 601, 1), "
            + "(29, 'game_interruption', 'raids', 'mods', 602, 1), "
            + "(35, 'game_interruption', 'scripting', 'mods', 603, 1), "
            + "(39, 'unlawful_activity', 'child_sexual_abuse', 'mods', 700, 1), "
            + "(40, 'unlawful_activity', 'unlawful_activity', 'mods', 701, 1));";

        private const string SEED =
            "INSERT IGNORE INTO cfh_topics (id, category, name, consequence, sort_order, enabled) VALUES "
            // help.cfh.reason.sexual_content
            + "(1, 'sexual_content', 'topic_1', '', 100, 1), "
            + "(2, 'sexual_content', 'topic_2', '', 101, 1), "
            + "(3, 'sexual_content', 'topic_3', '', 102, 1), "
            + "(30, 'sexual_content', 'topic_30', '', 103, 1), "
            + "(31, 'sexual_content', 'topic_31', '', 104, 1), "
            // help.cfh.reason.pii_meeting_irl
            + "(6, 'pii_meeting_irl', 'topic_6', '', 200, 1), "
            + "(8, 'pii_meeting_irl', 'topic_8', '', 201, 1), "
            // help.cfh.reason.scamming
            + "(9, 'scamming', 'topic_9', '', 300, 1), "
            + "(10, 'scamming', 'topic_10', '', 301, 1), "
            + "(11, 'scamming', 'topic_11', '', 302, 1), "
            + "(32, 'scamming', 'topic_32', '', 303, 1), "
            + "(33, 'scamming', 'topic_33', '', 304, 1), "
            // help.cfh.reason.trolling_bad_behavior
            + "(12, 'trolling_bad_behavior', 'bullying', '', 400, 1), "
            + "(13, 'trolling_bad_behavior', 'habbo_name', '', 401, 1), "
            + "(14, 'trolling_bad_behavior', 'topic_14', '', 402, 1), "
            + "(15, 'trolling_bad_behavior', 'topic_15', '', 403, 1), "
            + "(16, 'trolling_bad_behavior', 'topic_16', '', 404, 1), "
            + "(17, 'trolling_bad_behavior', 'topic_17', '', 405, 1), "
            + "(18, 'trolling_bad_behavior', 'topic_18', '', 406, 1), "
            + "(34, 'trolling_bad_behavior', 'inappropiate_room_group_event', '', 407, 1), "
            // help.cfh.reason.violent_behavior
            + "(19, 'violent_behavior', 'topic_19', '', 500, 1), "
            + "(20, 'violent_behavior', 'topic_20', '', 501, 1), "
            + "(21, 'violent_behavior', 'topic_21', '', 502, 1), "
            // help.cfh.reason.game_interruption
            + "(22, 'game_interruption', 'topic_22', '', 600, 1), "
            + "(23, 'game_interruption', 'topic_23', '', 601, 1), "
            + "(29, 'game_interruption', 'topic_29', '', 602, 1), "
            + "(35, 'game_interruption', 'topic_35', '', 603, 1), "
            // help.cfh.reason.unlawful_activity
            + "(39, 'unlawful_activity', 'topic_39', '', 700, 1), "
            + "(40, 'unlawful_activity', 'topic_40', '', 701, 1);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(REMOVE_EARLIER_SEED);
            migrationBuilder.Sql(SEED);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A seeded row is indistinguishable from an operator's once edited; it stays.
        }
    }
}
