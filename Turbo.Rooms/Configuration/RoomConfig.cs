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

    public int RoomCheckMs { get; init; } = 300000;
    public int RoomDeactivationDelayMs { get; init; } = 1800000;
    public int RoomTickMs { get; init; } = 50;
    public int AvatarTickMs { get; init; } = 500;
    public int RollerTickMs { get; init; } = 2000;
    public int DirtyItemsTickMs { get; init; } = 2000;
    public int MaxDirtyItemsPerFlush { get; init; } = 100;

    /// <summary>
    /// How many batches of <see cref="MaxDirtyItemsPerFlush"/> furni, pets and bots one
    /// persistence tick may write before it waits for the next. Deactivation writes everything.
    /// </summary>
    public int MaxDirtyBatchesPerFlush { get; init; } = 10;
    public int MaxTileHeightsPerFlush { get; init; } = 200;
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
    /// How long a cracked crackable stands in its last state before it is gone and its contents
    /// handed over. That state is its opening, which the asset plays once (the HC and BC boxes:
    /// eleven frames at a frame repeat of two, about 0.9 s) and then draws nothing.
    /// </summary>
    public int CrackableOpenMs { get; init; } = 1500;

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

    public int MannequinNameMaxLength { get; init; } = 32;

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

    public int RoomFilterMaxWords { get; init; } = 50;
    public int RoomFilterWordMaxLength { get; init; } = 30;

    public int BanHourMinutes { get; init; } = 60;
    public int BanDayMinutes { get; init; } = 1440;
    public int BanPermanentDays { get; init; } = 36500;
    public int ObjectDataMaxEntries { get; init; } = 32;
    public int ObjectDataMaxKeyLength { get; init; } = 64;
    public int ObjectDataMaxValueLength { get; init; } = 512;
    public int ChatLookAtRange { get; init; } = 6;
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

    public int GameDefaultDurationSeconds { get; init; } = 60;
    public int[] GameTeamEffectIds { get; init; } = [0, 33, 34, 35, 36];

    /// <summary>The team effects of a Battle Banzai team, by team (none, red, green, blue, yellow).</summary>
    public int[] BanzaiTeamEffectIds { get; init; } = [0, 33, 34, 35, 36];

    /// <summary>The team effects of a Freeze team, by team (none, red, green, blue, yellow).</summary>
    public int[] FreezeTeamEffectIds { get; init; } = [0, 40, 41, 42, 43];
}
