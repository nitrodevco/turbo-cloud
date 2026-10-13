namespace Turbo.Operations.Configuration;

/// <summary>
/// Tunables of the operator commands and the word filter. Every option carries the hotel default it ships with, so a
/// section left out of <c>appsettings.json</c> still starts.
/// </summary>
public class OperationsConfig
{
    public const string SECTION_NAME = "Turbo:Operations";

    /// <summary>
    /// How many minutes before a maintenance or a shutdown the hotel is reminded, besides when it
    /// is announced. A countdown shorter than one of these does not announce that one.
    /// </summary>
    public int[] CountdownReminderMinutes { get; init; } = [30, 15, 10, 5, 2, 1];

    /// <summary>How often the countdown looks at the clock, in milliseconds.</summary>
    public int CountdownTickMs { get; init; } = 1000;

    /// <summary>The minutes <c>:shutdown</c> counts down from when it is given none.</summary>
    public int DefaultShutdownMinutes { get; init; } = 5;

    /// <summary>The longest maintenance or shutdown countdown a command may start, in minutes.</summary>
    public int MaxCountdownMinutes { get; init; } = 1440;

    /// <summary>The most one <c>:give</c> may add or take, so a typo is not a hotel's economy.</summary>
    public int MaxCurrencyAmount { get; init; } = 10_000_000;

    /// <summary>The most items one <c>:giveitem</c> may add to an inventory.</summary>
    public int MaxGiveItemCount { get; init; } = 50;

    /// <summary>
    /// The longest note on a <c>:gift</c> tag, in characters; the catalog's gift dialog allows
    /// the same by default.
    /// </summary>
    public int MaxGiftMessageLength { get; init; } = 140;

    /// <summary>The most names <c>:online</c> lists before it says how many it left out.</summary>
    public int OnlineListMaxNames { get; init; } = 200;

    /// <summary>The longest alert or warning, in characters.</summary>
    public int MaxAlertLength { get; init; } = 500;

    /// <summary>
    /// What a filtered word is replaced with, by the hotel's word filter and by each room's own.
    /// </summary>
    public string WordFilterReplacement { get; init; } = "bobba";

    /// <summary>
    /// How many calls for help a player may have waiting for a moderator; one more is refused
    /// until staff close one. Keeps one player from filling the queue.
    /// </summary>
    public int CfhMaxOpenReports { get; init; } = 3;

    /// <summary>The most chat lines kept with one call for help.</summary>
    public int CfhMaxChatLines { get; init; } = 50;

    /// <summary>How many of their own reports a player's report status lists, newest first.</summary>
    public int CfhReportsListed { get; init; } = 50;

    /// <summary>The moderation tool's message templates (its send-message and mod action windows).</summary>
    public string[] ModerationMessageTemplates { get; init; } = [];

    /// <summary>The room tool's message templates.</summary>
    public string[] ModerationRoomMessageTemplates { get; init; } = [];

    /// <summary>How many chat lines the moderation tool's room chatlog shows, newest kept.</summary>
    public int ModerationRoomChatlogLines { get; init; } = 150;

    /// <summary>How many rooms a user chatlog covers, newest first.</summary>
    public int ModerationUserChatlogRooms { get; init; } = 10;

    /// <summary>How many chat lines a user chatlog shows per room.</summary>
    public int ModerationUserChatlogLines { get; init; } = 50;

    /// <summary>
    /// The name the room tool gives a room it renames; the default is the one its checkbox names
    /// (roomtool_frame).
    /// </summary>
    public string ModerationInappropriateRoomName { get; init; } =
        "Inappropriate to hotel management";

    /// <summary>
    /// What a caution, mute, kick or trade lock from the mod action window tells the player when
    /// the moderator wrote nothing (the window's message only "overrides default text"). Empty:
    /// such a caution is refused and the others are given without a message.
    /// </summary>
    public string ModerationDefaultSanctionMessage { get; init; } = string.Empty;

    /// <summary>User info's trade lock expiry for a lock without an end.</summary>
    public string ModerationPermanentTradeLockText { get; init; } = "Permanent";
}
