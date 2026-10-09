using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// The call for help topics, sent at login (<c>CfhTopicsInit</c>). The categories, topic ids,
    /// names and consequences are the set Habbo sent when PlusEMU's moderation_topics and
    /// moderation_topic_actions were captured: the ids are the client's help.cfh.topic.&lt;id&gt;
    /// texts, the categories its help.cfh.reason.&lt;name&gt; texts, and topic 34 is the
    /// inappropiate_room_group_event the client's room report asks for by name. INSERT IGNORE,
    /// so a hotel that changed a topic keeps it.
    /// </summary>
    public partial class AddCfhTopics : Migration
    {
        private const string SEED =
            "INSERT IGNORE INTO cfh_topics (id, category, name, consequence, sort_order, enabled) VALUES "
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
            + "(35, 'game_interruption', 'scripting', 'mods', 603, 1);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "cfh_topics",
                    columns: table => new
                    {
                        id = table.Column<int>(type: "int", nullable: false),
                        category = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        consequence = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        sort_order = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        enabled = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: true
                        ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_cfh_topics", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(SEED);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cfh_topics");
        }
    }
}
