namespace Turbo.Operations.Configuration;

/// <summary>
/// How long the hotel keeps each kind of record, in days; 0 keeps it for good. Under UK and EU
/// data protection law a record about a person is kept only as long as its purpose needs, and
/// each period here is that purpose's: a hotel that needs longer (a moderation backlog, a
/// dispute) or shorter sets its own, and says so in its privacy notice. The job that removes old
/// rows runs every <see cref="IntervalMinutes"/> and takes at most <see cref="BatchSize"/> rows
/// of a table at a time, so it never holds a long lock.
/// </summary>
public sealed class RetentionConfig
{
    public const string SECTION_NAME = "Turbo:Retention";

    public int IntervalMinutes { get; init; } = 60;

    public int BatchSize { get; init; } = 5000;

    /// <summary>Room chat and private messages: the evidence a report is checked against.</summary>
    public int ChatDays { get; init; } = 90;

    /// <summary>Who entered which room: visits behind a report, and the navigator's history.</summary>
    public int RoomEntryDays { get; init; } = 90;

    /// <summary>Logins with their address and machine: finding a banned player's other accounts.</summary>
    public int LoginDays { get; init; } = 180;

    /// <summary>Every balance change: disputed charges, refunds and duplicated rewards.</summary>
    public int CurrencyLedgerDays { get; init; } = 365;

    /// <summary>Furni changing hands in trades: tracing an item a player says was taken.</summary>
    public int FurnitureTransferDays { get; init; } = 730;

    /// <summary>Commands staff and players used: what staff did, and when.</summary>
    public int CommandLogDays { get; init; } = 365;

    /// <summary>Who respected whom: spotting respect farming between accounts.</summary>
    public int RespectDays { get; init; } = 90;

    /// <summary>
    /// The records that only make a retried action safe (a respect's counters, a pet fed, a
    /// referenced credit): a retry comes within minutes, so a month is plenty.
    /// </summary>
    public int ReceiptDays { get; init; } = 30;

    /// <summary>Daily tasks of days gone by.</summary>
    public int DailyTaskDays { get; init; } = 30;

    /// <summary>Wired chest deposits and withdrawals, which the chest's log shows.</summary>
    public int WiredChestDays { get; init; } = 180;

    /// <summary>Calls for help, once staff closed them.</summary>
    public int ClosedReportDays { get; init; } = 365;

    /// <summary>Room bans and mutes that have run out, which nothing reads again.</summary>
    public int ExpiredRoomSanctionDays { get; init; } = 7;

    /// <summary>Room events that have ended.</summary>
    public int EndedRoomEventDays { get; init; } = 30;

    /// <summary>Asset publish runs, which the panel lists.</summary>
    public int AssetPublishDays { get; init; } = 180;

    /// <summary>The catalogue backups taken before each restore; ones made by hand are kept.</summary>
    public int AutomaticCatalogBackupsKept { get; init; } = 20;
}
