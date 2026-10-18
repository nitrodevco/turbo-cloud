using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Configuration;

public class RoomConfig
{
    public const string SECTION_NAME = "Turbo:Rooms";

    public Altitude MaxStackHeight { get; init; } = Altitude.FromInt(4000);
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;

    // FloorHeightMap uses -1 for automatic walls above the highest floor; zero is fixed.
    public int DefaultWallHeight { get; init; } = -1;
    public Altitude MaxStepHeight { get; init; } = Altitude.FromInt(200);
    public bool PlaceItemsOnAvatars { get; init; } = true;
    public bool EnableDiagonalChecking { get; init; } = true;
    public Dictionary<string, string> WaterAreaLogicByDefinition { get; init; } =
        new()
        {
            ["bw_water_1"] = "furniture_water_area",
            ["bw_water_2"] = "furniture_water_area",
            ["val13_water"] = "furniture_water_area",
            ["stackable_water"] = "furniture_water_area",
        };
    public int MaxPlayersLimit { get; init; } = 50;
    public int RoomNameMaxLength { get; init; } = 60;
    public int RoomDescriptionMaxLength { get; init; } = 128;
    public int RoomPasswordMaxLength { get; init; } = 64;
    public int RoomTagsMax { get; init; } = 2;
    public int RoomTagMaxLength { get; init; } = 30;
    public int RoomIdleSleepTimeoutMinSeconds { get; init; } = 60;
    public int RoomIdleSleepTimeoutMaxSeconds { get; init; } = 3600;
    public int RoomIdleAutokickTimeoutMinSeconds { get; init; } = 60;
    public int RoomIdleAutokickTimeoutMaxSeconds { get; init; } = 86400;

    /// <summary>
    /// Listing changes the room directory remembers for navigator caches. A silo that falls
    /// further behind than this drops its whole cache instead.
    /// </summary>
    public int ListingChangeLogSize { get; init; } = 10000;

    /// <summary>
    /// How far back the room directory counts the players who entered each active room, which
    /// the navigator's hot rooms (<c>hot</c>) are ordered by. Habbo's hot rooms are led by the
    /// rooms that just filled up rather than the fullest ones (official client capture
    /// 2026-10-16, three hotel views 32 minutes apart); the window length is inference.
    /// </summary>
    public int HotRoomWindowSeconds { get; init; } = 600;

    public int RoomCheckMs { get; init; } = 300000;
    public int RoomDeactivationDelayMs { get; init; } = 1800000;

    /// <summary>
    /// How long after a wired menu Reload or Rollback sends everyone out they are sent back in.
    /// Habbo forwards about two seconds after the close (official client capture, 2026-10-16:
    /// 2001 ms and 2005 ms).
    /// </summary>
    public int RoomStateReturnDelayMs { get; init; } = 2000;
    public int RoomTickMs { get; init; } = 50;
    public int AvatarTickMs { get; init; } = 500;

    /// <summary>
    /// How often rollers move in a room without a Roller Speed Controller (official client
    /// capture, 2026-10-18: 2000 ms, n=6, the controller's slowest speed).
    /// </summary>
    public int RollerTickMs { get; init; } = 2000;

    /// <summary>
    /// The cycle roller moves fall on: a room's rollers move on every cycle, or every few, at the
    /// speed it runs at, so a new speed takes over from the next move (official client capture,
    /// 2026-10-18: every move 500 ms apart or a multiple of it).
    /// </summary>
    public int RollerStepMs { get; init; } = 500;

    /// <summary>
    /// How often rollers move at each speed of a Roller Speed Controller (<c>conf_queue_speed</c>),
    /// from the speed it is placed at (official client capture, 2026-10-18: 500, 1000, 1500 and
    /// 2000 ms, n=45/27/5/4).
    /// </summary>
    public int[] RollerSpeedControlTickMs { get; init; } = [500, 1000, 1500, 2000];

    /// <summary>
    /// How many Roller Speed Controllers a room takes (official client capture, 2026-10-18: a
    /// second is refused with <c>room.error.type_limit</c>, 1/1).
    /// </summary>
    public int MaxRollerSpeedControls { get; init; } = 1;
    public int DirtyItemsTickMs { get; init; } = 2000;
    public int MaxDirtyItemsPerFlush { get; init; } = 100;

    /// <summary>
    /// How many batches of <see cref="MaxDirtyItemsPerFlush"/> furni, pets and bots one
    /// persistence tick may write before it waits for the next. Deactivation writes everything.
    /// </summary>
    public int MaxDirtyBatchesPerFlush { get; init; } = 10;
    public int MaxTileHeightsPerFlush { get; init; } = 200;

    /// <summary>
    /// The bit of a <c>HeightMap</c> / <c>HeightMapUpdate</c> tile value that marks a tile nothing
    /// may be stacked on; the bits below it are the height × 256. Habbo uses 15 (its client
    /// variable <c>room.stacking_blocked_mask_bit=15</c>), which lets stacks go up to 127.99; the
    /// client must decode with the same bit.
    /// </summary>
    public int StackingBlockedMaskBit { get; init; } = 15;
    public int MaxPathNodes { get; init; } = 4096;

    /// <summary>
    /// How long a pet or bot that found no way to the avatar it follows waits before searching
    /// again, when that avatar has not moved. It searches at once when the avatar moves.
    /// </summary>
    public int NpcFollowRetryMs { get; init; } = 2000;

    public int ChatMaxLength { get; init; } = 100;

    /// <summary>How long a thrown dice shows the rolling animation before landing.</summary>
    public int DiceRollMs { get; init; } = 3000;

    /// <summary>
    /// The delay a cracked crackable's removal carries, which the client waits before it takes
    /// the furni away (RoomMessageHandler.onObjectRemove), so the opening - its last state, which
    /// the asset plays once and then draws nothing - is seen. The server removes it and hands its
    /// contents over at once. Habbo sends 1575 ms on the Habbo Club box.
    /// </summary>
    public int CrackableRemoveDelayMs { get; init; } = 1575;

    /// <summary>
    /// How long a present stands in its opening state before it is gone and the owner is told what
    /// it held. Measured on Habbo: the "you open the gift" card follows the glow by 1.98 s and
    /// 2.09 s, while the gift wrapped box bursts into its sparkles and confetti.
    /// </summary>
    public int PresentOpenMs { get; init; } = 2000;

    /// <summary>
    /// How long a vending machine shows its dispensing state (state 1, which a fridge's or a drinks
    /// machine's asset animates) after handing an item over.
    /// </summary>
    public int VendingDispenseMs { get; init; } = 1000;

    /// <summary>How long a vending machine waits for an avatar it sent walking over to arrive.</summary>
    public int VendingWalkTimeoutMs { get; init; } = 15000;

    /// <summary>How long a random teleport tile, and the one it sent an avatar to, stay lit.</summary>
    public int RandomTeleportFlashMs { get; init; } = 1000;

    /// <summary>How long the wheel of fortune spins before stopping on a segment.</summary>
    public int WheelSpinMs { get; init; } = 5000;

    /// <summary>Delay before a one-way door closes behind the avatar that entered it.</summary>
    public int OneWayDoorCloseMs { get; init; } = 2000;

    /// <summary>
    /// How long each step of a teleport shows: open, shut with the player inside, flashing,
    /// open again at the other half.
    /// </summary>
    public int TeleportStepMs { get; init; } = 500;

    /// <summary>
    /// How long a teleporter waits for a player to walk up to it, into it or out of it before
    /// it gives up and lets them go.
    /// </summary>
    public int TeleportWalkTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// How long a teleporter holds a player it forwarded to another room. If they are still
    /// here after this (the room refused them), it opens and lets them back out.
    /// </summary>
    public int TeleportForwardTimeoutMs { get; init; } = 10000;

    public int StickieTextMaxLength { get; init; } = 500;

    /// <summary>How long a hand item (drink, snack) stays in an avatar's hand.</summary>
    public int HandItemExpireMs { get; init; } = 240000;

    /// <summary>
    /// How long the room has to answer a word quiz question: 15000 ms in the official client capture
    /// of 2026-10-12 (each QuestionFinished 15 s after its Question).
    /// </summary>
    public int WordQuizDurationMs { get; init; } = 15000;

    public int MannequinNameMaxLength { get; init; } = 32;

    /// <summary>
    /// Which palette each recolorable furni class takes, as the client's
    /// <c>recolorable_bc.palette_to_furni</c>: <c>palette=class1,class2;...</c>. A class it does
    /// not name takes the palette <c>default</c>. Link that external variable to this setting so
    /// the recolor window and the server agree.
    /// </summary>
    public string RecolorablePaletteToFurni { get; init; } =
        "default=bc_block_0_recolorable,bc_block_1_recolorable,bc_cone_recolorable,bc_cylinder_recolorable,bc_halfcylinder_recolorable,bc_hemisphere_recolorable,bc_panel_recolorable,bc_pyramidtip_recolorable,bc_quarterring_recolorable,bc_round_recolorable,bc_stairs_recolorable,bc_standinghalfcylinder_recolorable,bc_standingtriangularprism_recolorable,bc_stick_recolorable,bc_triangularprism_recolorable,bc_wedge_recolorable,bc_block_small_recolorable,bc_curvedramp_recolorable,bc_glasspanel_recolorable,bc_quartercircle_recolorable,bc_ramp_recolorable,bc_sphere_recolorable,bc_sphere_small_recolorable,bc_wedgecrnr_recolorable,bc_alpha1_alpha_recolorable,bc_alpha1_a_recolorable,bc_alpha1_b_recolorable,bc_alpha1_c_recolorable,bc_alpha1_d_recolorable,bc_alpha1_e_recolorable,bc_alpha1_f_recolorable,bc_alpha1_g_recolorable,bc_alpha1_h_recolorable,bc_alpha1_i_recolorable,bc_alpha1_j_recolorable,bc_alpha1_k_recolorable,bc_alpha1_l_recolorable,bc_alpha1_m_recolorable,bc_alpha1_n_recolorable,bc_alpha1_num_recolorable,bc_alpha1_o_recolorable,bc_alpha1_p_recolorable,bc_alpha1_q_recolorable,bc_alpha1_r_recolorable,bc_alpha1_s_recolorable,bc_alpha1_t_recolorable,bc_alpha1_u_recolorable,bc_alpha1_v_recolorable,bc_alpha1_w_recolorable,bc_alpha1_x_recolorable,bc_alpha1_y_recolorable,bc_alpha1_z_recolorable;"
        + "artdeco1=bc_block_artdeco1_recolorable;"
        + "classic=bc_block_glass_recolorable,bc_block_marble_recolorable,bc_block_wool_recolorable,bc_flowerhedge_recolorable,bc_lavarock_recolorable,bc_terra_recolorable,bc_tile_recolorable;"
        + "grass=bc_block_grass_recolorable;"
        + "industrial=bc_block_industrial_recolorable;"
        + "metal=bc_block_metalgrip_recolorable;"
        + "redbrick=bc_block_redbrick_recolorable;"
        + "sand=bc_block_sand_recolorable;"
        + "water=bc_block_water_recolorable;"
        + "metalcrate=bc_metalcrate_recolorable;"
        + "stone=bc_stone_recolorable;"
        + "wood=bc_wood_recolorable";

    /// <summary>
    /// The recolorable palettes by name, each <c>RRGGBB,...</c> as the client's
    /// <c>recolorable_bc.palette.&lt;name&gt;</c>. A furni is only recolored, or placed with a
    /// colour, from its palette; with none set it keeps white. The defaults are Habbo's
    /// (external variables of WIN63-202610121624).
    /// </summary>
    public Dictionary<string, string> RecolorablePalettes { get; init; } =
        new()
        {
            ["artdeco1"] = "672913,A73E18,EB840C,E9C258,669933",
            ["classic"] =
                "FFFFFF,F7EBBC,ABD0D2,5EAAF8,835AB1,AC5AB1,FF99BC,525252,672913,E14218,FF9900,FFD837,92D13D,007223",
            ["default"] =
                "FFFFFF,AEF0D8,99FFCC,00E9E5,00F8FD,A1E8FD,ABD0D2,BCC0C4,F5D1B3,FDE1B1,F7EBBC,FDD97E,FFD837,DBF96F,BAFA00,9ABD00,8AAE00,92D13D,B6FA70,4EFA99,52DA50,00BA00,00AA00,00A943,007900,007223,005900,2E632E,0D380C,336666,004059,525252,778084,008895,2CBFFF,5EAAF8,BCB7FF,DDB6FE,FEB6FE,F5BFE1,FF99BC,FF7852,E14218,FF3600,E76000,FF9900,FEA13D,AD8100,666600,513100,6F3D0C,672913,8D1600,AE0024,E7005F,FF549C,AC5AB1,835AB1,6685FF,0085FF,0074FF,9C73FF,FE72FF,DE00D7,96008E,3F1BC7,0000C8,683EFF,0000FF",
            ["grass"] = "B0B1AE,6AB18E,57832E,30B106,929E12,B19626,B15501",
            ["industrial"] = "545B5B,3C412F,3D3432,262C31,383F55",
            ["metal"] = "8F9B9B,676F51,695955,414B53,606B92",
            ["metalcrate"] =
                "BBBBBB,B5AC8A,7D989A,457DB6,604282,7E4282,BB708A,3C3C3C,4B1E0E,A53012,BB7000,BB9E28,6B992D,00541A",
            ["redbrick"] =
                "CECECE,C7BE98,8AA8A9,4C89C8,6A498F,8B498F,CE7B98,424242,53210F,B63513,CE7B00,CEAE2C,76A931,005C1C",
            ["sand"] = "F7EBBC,C29655,7E5C1D,95701D,CB8233,E1A53A,F0C833",
            ["stone"] =
                "D0D0D0,C9C099,8BAAAB,4D8BCA,6B4990,8C4990,D07D99,434343,54210F,B73614,D07D00,D0B02D,77AA32,005D1D",
            ["water"] = "336666,0069A6,5EAAF8,7AD8FF,99FFCC,E14218",
            ["wood"] = "636568,667660,5A5045,7F603B,714A23,7D3822",
        };

    /// <summary>Longest figure string a furni stores (a clothing booth's look).</summary>
    public int FigureMaxLength { get; init; } = 300;

    /// <summary>
    /// Temporary furni (placed by wired, gone when the room unloads) a room may hold at once.
    /// They cost nobody anything, so without a cap a looping stack fills the room.
    /// </summary>
    public int TemporaryFurniMax { get; init; } = 200;

    /// <summary>
    /// Whether Builders Club furni may be borrowed into a group room by someone who does not own
    /// it. The client greys out its own drag handle from
    /// <c>builders.club.furniture.placement.group.room.enabled</c>, so the two are expected to
    /// agree.
    /// </summary>
    public bool BuildersClubInGroupRooms { get; init; } = true;

    /// <summary>
    /// Whether a player the owner gave plain room rights (controller level 1, a room without a
    /// group) may place furni from their inventory there. Off is Habbo: the placement is refused
    /// with "You don't have rights to put furniture in here!", while they still move, turn and
    /// use the room's furni. Habbo refused seven placements so, by four accounts on two machines,
    /// one in the room of a Habbo Club owner and one by a group member (official client captures,
    /// 2026-10-13); its hotel text says "In group homerooms room rights are determined by group
    /// membership of a user": placing in someone else's room is the group's to allow. A group
    /// homeroom is not affected (the group's admins, and its members when it lets them decorate,
    /// place there), nor are the owner and staff. On lets plain rights place furni as well.
    /// </summary>
    public bool RightsHoldersCanPlaceFurni { get; init; } = false;

    /// <summary>
    /// The longest a saved floor plan may be on either axis. The editor refuses to draw past 64
    /// (<c>FloorPlanCache.MAX_AXIS_LENGTH</c>), so raising this past that gains nothing.
    /// </summary>
    public int FloorPlanMaxAxisLength { get; init; } = 64;

    /// <summary>
    /// The largest area a saved floor plan may cover, counted as the editor counts it:
    /// <c>(width - 1) * (height - 1)</c>. The editor lets a player past this only when they hold
    /// the <c>BUILDER_AT_WORK</c> perk, which the <c>room.floorplan.large</c> permission projects
    /// to; the server lets the same players past it, up to the axis limit above.
    /// </summary>
    public int FloorPlanMaxArea { get; init; } = 3025;

    /// <summary>
    /// The highest fixed wall height the editor's slider can reach. It draws the value plus one,
    /// so 15 is the "16" a player sees.
    /// </summary>
    public int FloorPlanMaxWallHeight { get; init; } = 15;

    /// <summary>
    /// How long after a saved floor plan sends everyone in the room out they are forwarded back
    /// in. Habbo forwards about two seconds after the close (official client capture, 2026-10-17:
    /// six saves, ~2.0 s each).
    /// </summary>
    public int FloorPlanSaveReturnDelayMs { get; init; } = 2000;

    /// <summary>What renting a rentable space costs, in credits. Zero makes them free.</summary>
    public int RentableSpacePriceCredits { get; init; } = 10;

    /// <summary>How long a rent lasts. A week by default.</summary>
    public int RentableSpaceDurationSeconds { get; init; } = 604800;

    /// <summary>
    /// The playlists a video display offers. Which videos a hotel shows is hotel data, so it
    /// ships empty; a video's length is needed because the client never says when one ends.
    /// </summary>
    public List<YoutubePlaylistConfig> YoutubePlaylists { get; init; } = [];

    /// <summary>
    /// How many song disks a jukebox's playlist takes. The client is told the limit with every
    /// playlist and alerts when a disk is refused for it.
    /// </summary>
    public int JukeboxMaxDisks { get; init; } = 10;

    /// <summary>Largest side, in tiles, an area hider may cover.</summary>
    public int AreaHideMaxSize { get; init; } = 20;

    /// <summary>How long both sides of a love lock have to confirm before it is cancelled.</summary>
    public int FriendFurniLockTimeoutMs { get; init; } = 60000;

    /// <summary>Definition (class) name of the note a post-it wall creates.</summary>
    public string SpamWallPostItDefinitionName { get; init; } = "post_it";

    /// <summary>Items one side may offer in a trade.</summary>
    public int TradeMaxItemsPerSide { get; init; } = 50;

    /// <summary>
    /// How long an open trade keeps the room's trade grain loaded past its last action. A trade is
    /// memory only, so a grain collected for idleness while two players think would close it.
    /// </summary>
    public int TradeKeepAliveMinutes { get; init; } = 30;

    public int RoomFilterMaxWords { get; init; } = 50;
    public int RoomFilterWordMaxLength { get; init; } = 30;

    public int BanHourMinutes { get; init; } = 60;
    public int BanDayMinutes { get; init; } = 1440;
    public int BanPermanentDays { get; init; } = 36500;
    public int ObjectDataMaxEntries { get; init; } = 32;
    public int ObjectDataMaxKeyLength { get; init; } = 64;
    public int ObjectDataMaxValueLength { get; init; } = 512;
    public int ChatLookAtRange { get; init; } = 0;
    public int ChatLookAtRevertMs { get; init; } = 5000;
    public int ChatFloodMaxMessagesExtraSensitivity { get; init; } = 4;
    public int ChatFloodMaxMessagesNormalSensitivity { get; init; } = 6;
    public int ChatFloodMaxMessagesMinimalSensitivity { get; init; } = 8;
    public int ChatFloodWindowMs { get; init; } = 4000;
    public int ChatFloodMuteMs { get; init; } = 30000;
    public int ChatMuteMaxDurationMinutes { get; init; } = 60;
    public bool ChatlogEnabled { get; init; } = true;
    public int ChatlogTickMs { get; init; } = 5000;
    public int MaxChatlogsPerFlush { get; init; } = 200;
    public int MaxPendingChatlogs { get; init; } = 2000;

    /// <summary>
    /// A chat command runs inside the room's turn, so one that holds it longer than this is logged
    /// as a warning with its name.
    /// </summary>
    public int CommandSlowWarningMs { get; init; } = 250;

    /// <summary>How long <c>:mute</c> mutes for when the player names no minutes.</summary>
    public int CommandDefaultMuteMinutes { get; init; } = 10;

    /// <summary>
    /// Command uses a room keeps, and a persistence grain keeps, while waiting to be written; the
    /// oldest go first when a database outage fills the buffer.
    /// </summary>
    public int MaxPendingCommandLogs { get; init; } = 500;

    public int MaxCommandLogsPerFlush { get; init; } = 200;

    /// <summary>A game timer's time before anyone sets it: the official Banzai counter shows 00:30.</summary>
    public int GameDefaultDurationSeconds { get; init; } = 30;
    public int[] GameTeamEffectIds { get; init; } = [0, 33, 34, 35, 36];

    /// <summary>The team effects of a Battle Banzai team, by team (none, red, green, blue, yellow).</summary>
    public int[] BanzaiTeamEffectIds { get; init; } = [0, 33, 34, 35, 36];

    /// <summary>The team effects of a Freeze team, by team (none, red, green, blue, yellow).</summary>
    public int[] FreezeTeamEffectIds { get; init; } = [0, 40, 41, 42, 43];

    /// <summary>
    /// Whether owners may manage raid protection. Off, nobody is told they can and a save is
    /// refused with "Raid protection is currently disabled".
    /// </summary>
    public bool RaidProtectionEnabled { get; init; } = true;

    /// <summary>
    /// The lowest controller level told it may manage raid protection, and allowed to read and
    /// save it. Habbo tells every player with rights in the room it may (official client
    /// captures: rights 2 of 2, group admin 1 of 1, owner 25 of 25; none 12 of 12 told not).
    /// </summary>
    public RoomControllerType RaidProtectionManageLevel { get; init; } = RoomControllerType.Rights;

    /// <summary>
    /// The temporary ban lengths raid protection accepts: the client's ban length menu
    /// (<c>RaidProtectionSettingsController.BAN_DURATION_VALUES</c>), 5 minutes to 1 week.
    /// </summary>
    public int[] RaidBanDurationsSeconds { get; init; } =
    [300, 900, 1800, 3600, 10800, 21600, 43200, 86400, 259200, 604800];

    /// <summary>
    /// How long the door guard may block suspicious visitors: the client's menu
    /// (<c>GUARD_DURATION_VALUES</c>), 5 minutes to 3 hours.
    /// </summary>
    public int[] RaidGuardDurationsSeconds { get; init; } = [300, 900, 1800, 3600, 10800];

    /// <summary>
    /// How high above the floor an avatar sits when it sits down where it stands (the Sit action):
    /// Habbo sends <c>sit 0.55</c> (recorded on hhus, 2026-10-12).
    /// </summary>
    public double FloorSitHeight { get; init; } = 0.55;

    /// <summary>
    /// How long after sending a player a room's ban list their next request for it goes
    /// unanswered, as Habbo lets the client's immediate repeats go (it asks again whenever the
    /// list arrives empty). 0 answers every request.
    /// </summary>
    public int BanListRepeatIgnoreMs { get; init; } = 1000;

    /// <summary>
    /// Furniture class names the client draws with another class's assets (FurnitureAliases,
    /// asked for at login): name to the class whose assets it uses. Empty by default. Habbo
    /// sends 14, its advertising furni drawn with their campaign assets
    /// (recorded on hhus, 2026-10-12).
    /// </summary>
    /// <remarks>
    /// Habbo's: footylamp_campaign = footylamp_campaign_ing, ads_711 = ads_711c,
    /// ads_711shelf = ads_711shelfcmp, ads_oc_soda = ads_oc_soda_cmp, ads_boost_surfb =
    /// ads_boost_surfbcmp, ads_target_wall = ads_target_wall_cmp, ads_capri_chair =
    /// ads_capri_chair_camp, ads_capri_lava = ads_capri_lava_camp, ads_capri_arcade =
    /// ads_capri_arcade_camp, ads_pepsi0 = ads_pepsi0_camp, ads_oc_soda_cherry =
    /// ads_oc_soda_cherry_cmp, ads_rangocactus = ads_rangocactus_camp, ads_wowpball =
    /// ads_wowpball_camp, ads_alvinsurf = ads_alvinsurfcmp.
    /// </remarks>
    public Dictionary<string, string> FurnitureAliases { get; init; } = [];
}
