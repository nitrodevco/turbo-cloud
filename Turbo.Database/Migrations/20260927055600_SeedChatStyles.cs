using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Seeds <c>player_chat_styles</c> with every bubble the client ships (its
    /// <c>chatstyles_xml</c>), flagged as the client flags them: <c>systemStyle</c> is system,
    /// <c>hcOnly</c> club-only, <c>ambassadorOnly</c> ambassador-only and <c>purchasable</c>
    /// purchasable. The NFT styles (1000-9999) are purchasable too, since the client offers them
    /// only to an account that holds one, and the <c>staff</c> bubble is staff-only, which the
    /// client leaves to the server. A row already present takes these flags, as the columns are
    /// new with <c>AddChatStylePermissions</c>.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260927055600_SeedChatStyles")]
    public partial class SeedChatStyles : Migration
    {
        private const string STATEMENT =
            "INSERT INTO player_chat_styles (client_style_id, name, club_only, ambassador_only, staff_only, purchasable, `system`) VALUES (0, 'normal', 0, 0, 0, 0, 0), (1, 'generic', 0, 0, 0, 0, 1), (2, 'bot', 0, 0, 0, 0, 1), (3, 'normal_red', 0, 0, 0, 0, 0), (4, 'normal_blue', 0, 0, 0, 0, 0), (5, 'normal_yellow', 0, 0, 0, 0, 0), (6, 'normal_green', 0, 0, 0, 0, 0), (7, 'normal_grey', 0, 0, 0, 0, 0), (8, 'fortune_teller', 0, 0, 0, 0, 1), (9, 'zombie_hand', 1, 0, 0, 0, 0), (10, 'skeleton', 1, 0, 0, 0, 0), (11, 'normal_sky_blue', 1, 0, 0, 0, 0), (12, 'normal_pink', 1, 0, 0, 0, 0), (13, 'normal_purple', 1, 0, 0, 0, 0), (14, 'normal_dark_yellow', 1, 0, 0, 0, 0), (15, 'normal_dark_turquoise', 1, 0, 0, 0, 0), (16, 'hearts', 1, 0, 0, 0, 0), (17, 'gothicrose', 1, 0, 0, 0, 0), (19, 'piglet', 1, 0, 0, 0, 0), (20, 'sausagedog', 1, 0, 0, 0, 0), (21, 'firingmylazer', 1, 0, 0, 0, 0), (22, 'dragon', 1, 0, 0, 0, 0), (23, 'staff', 0, 0, 1, 0, 0), (24, 'bats', 1, 0, 0, 0, 0), (25, 'console', 1, 0, 0, 0, 0), (26, 'steampunk_pipe', 1, 0, 0, 0, 0), (27, 'storm', 1, 0, 0, 0, 0), (28, 'parrot', 0, 0, 0, 0, 1), (29, 'pirate', 1, 0, 0, 0, 0), (30, 'bot_guide', 0, 0, 0, 0, 1), (31, 'bot_rentable', 0, 0, 0, 0, 1), (32, 'skelestock', 0, 0, 0, 0, 1), (33, 'bot_frank_large', 0, 0, 0, 0, 1), (34, 'notification', 0, 0, 0, 0, 1), (35, 'goat', 0, 0, 0, 0, 1), (36, 'santa', 0, 0, 0, 0, 1), (37, 'ambassador', 0, 1, 0, 0, 0), (38, 'radio', 0, 0, 0, 0, 1), (120, 'snowstorm_red', 0, 0, 0, 0, 1), (121, 'snowstorm_blue', 0, 0, 0, 0, 1), (130, 'wired_team_red', 0, 0, 0, 0, 1), (131, 'wired_team_blue', 0, 0, 0, 0, 1), (132, 'wired_team_yellow', 0, 0, 0, 0, 1), (133, 'wired_team_green', 0, 0, 0, 0, 1), (200, 'notification_red', 0, 0, 0, 0, 1), (201, 'notification_green', 0, 0, 0, 0, 1), (202, 'notification_blue', 0, 0, 0, 0, 1), (210, 'notification_alert', 0, 0, 0, 0, 1), (211, 'notification_info', 0, 0, 0, 0, 1), (212, 'notification_warning', 0, 0, 0, 0, 1), (220, 'notification_wrong', 0, 0, 0, 0, 1), (221, 'notification_wrong_circle', 0, 0, 0, 0, 1), (222, 'notification_correct', 0, 0, 0, 0, 1), (223, 'notification_correct_circle', 0, 0, 0, 0, 1), (224, 'notification_question_mark', 0, 0, 0, 0, 1), (225, 'notification_question_mark_circle', 0, 0, 0, 0, 1), (226, 'notification_arrow_up', 0, 0, 0, 0, 1), (227, 'notification_arrow_up_circle', 0, 0, 0, 0, 1), (228, 'notification_arrow_down', 0, 0, 0, 0, 1), (229, 'notification_arrow_down_circle', 0, 0, 0, 0, 1), (250, 'notification_skull', 0, 0, 0, 0, 1), (251, 'notification_skull_2', 0, 0, 0, 0, 1), (252, 'notification_magnifier', 0, 0, 0, 0, 1), (1000, 'nft_habbo_avatar_bronze', 0, 0, 0, 1, 0), (1001, 'nft_habbo_avatar_gold', 0, 0, 0, 1, 0), (1002, 'nft_habbo_avatar_diamond', 0, 0, 0, 1, 0), (1003, 'nft_habbo_avatar_rainbow', 0, 0, 0, 1, 0), (1004, 'nft_habbo_avatar_trippy', 0, 0, 0, 1, 0), (1005, 'nft_habbo_avatar_ultra_trippy', 0, 0, 0, 1, 0), (1006, 'nft_mvhq', 0, 0, 0, 1, 0), (1007, 'nft_metakey', 0, 0, 0, 1, 0), (1010, 'nft_crafted_habbo_avatar', 0, 0, 0, 1, 0), (1011, 'nft_balloon_orange', 0, 0, 0, 1, 0), (1012, 'nft_balloon_blue', 0, 0, 0, 1, 0), (1013, 'nft_origami_orange', 0, 0, 0, 1, 0), (1014, 'nft_origami_blue', 0, 0, 0, 1, 0), (1015, 'nft_chocolate_dark', 0, 0, 0, 1, 0), (1016, 'nft_chocolate_white', 0, 0, 0, 1, 0), (1017, 'nft_clay', 0, 0, 0, 1, 0), (1018, 'nft_scroll', 0, 0, 0, 1, 0), (1019, 'nft_pillow', 0, 0, 0, 1, 0), (1020, 'nft_bobba', 0, 0, 0, 1, 0), (1021, 'nft_pinktube', 0, 0, 0, 1, 0), (1022, 'nft_keycaps', 0, 0, 0, 1, 0), (1023, 'nft_xmas22', 0, 0, 0, 1, 0), (1024, 'nft_rocky', 0, 0, 0, 1, 0), (1025, 'nft_ice', 0, 0, 0, 1, 0), (1026, 'nft_aurora', 0, 0, 0, 1, 0), (1027, 'nft_money', 0, 0, 0, 1, 0), (10000, 'recycled', 0, 0, 0, 1, 0) ON DUPLICATE KEY UPDATE name = VALUES(name), club_only = VALUES(club_only), ambassador_only = VALUES(ambassador_only), staff_only = VALUES(staff_only), purchasable = VALUES(purchasable), `system` = VALUES(`system`);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(STATEMENT);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are indistinguishable from operator rows once edited; they stay.
        }
    }
}
