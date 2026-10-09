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
}
