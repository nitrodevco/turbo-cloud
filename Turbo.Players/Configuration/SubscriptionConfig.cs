namespace Turbo.Players.Configuration;

/// <summary>
/// Habbo Club and Builders Club tunables, read by <c>PlayerSubscriptionGrain</c>. They live on
/// the player module because a subscription is a player's, and because the grain that enforces a
/// limit has to read it from its own module's config.
/// </summary>
public class SubscriptionConfig
{
    /// <summary>
    /// Days a lapsed subscription stays in grace. The client shows the grace state for as long as
    /// <c>secondsLeftWithGrace</c> outlives <c>secondsLeft</c>.
    /// </summary>
    public int GraceDays { get; init; } = 3;

    /// <summary>
    /// Days in one membership period. The client hardcodes 31 when it adds a subscription back up
    /// (<c>ClubBuyCatalogWidget</c> draws <c>months * 31 + extraDays</c>), so changing this makes
    /// the purse disagree with the server.
    /// </summary>
    public int DaysPerPeriod { get; init; } = 31;

    /// <summary>
    /// The product code the client is told the Habbo Club membership is. It only acts on
    /// <c>habbo_club</c> and <c>club_habbo</c>, which are the two it forwards to the web page.
    /// </summary>
    public string HabboClubProductName { get; init; } = "club_habbo";

    /// <summary>
    /// Days of used-up Habbo Club membership that earn one club gift. The client draws the
    /// remainder in months of 31, so anything else makes its countdown read oddly.
    /// </summary>
    public int ClubGiftIntervalDays { get; init; } = 31;

    /// <summary>Items a Builders Club member may borrow at once on their first purchase.</summary>
    public int BuildersClubBaseFurniLimit { get; init; } = 100;

    /// <summary>What each further Builders Club purchase adds to that limit.</summary>
    public int BuildersClubFurniLimitPerExtension { get; init; } = 100;

    /// <summary>The ceiling those extensions raise the limit to.</summary>
    public int BuildersClubMaxFurniLimit { get; init; } = 1000;

    /// <summary>
    /// Items someone with no Builders Club membership may borrow. The client lets a trial member
    /// build, at the cost of hiding the room, and blocks them at this many items.
    /// </summary>
    public int BuildersClubTrialFurniLimit { get; init; } = 10;

    /// <summary>
    /// What the client is shown is left of a Habbo Club membership held by a permanent
    /// <c>club.habbo_club.unlimited</c>, counted from now, so it never comes closer. Only shown:
    /// the server counts them a member for as long as they hold the node. A node that runs out
    /// shows its own end instead.
    /// </summary>
    public int PermanentHabboClubDisplayDays { get; init; } = 7;

    /// <summary>The same for Builders Club (<c>club.builders_club.unlimited</c>), in hours.</summary>
    public int PermanentBuildersClubDisplayHours { get; init; } = 24;

    /// <summary>
    /// The share of a month's catalog spend the club centre says HC Payday gives back
    /// (<c>hccenter.breakdown.paydayfactor</c>). Habbo's is 0.1 (recorded on hhus, 2026-10-13).
    /// </summary>
    public double PaydayKickbackPercentage { get; init; } = 0.1;

    /// <summary>
    /// Minutes the club centre counts down to HC Payday. Turbo pays no payday yet, so this is the
    /// countdown Habbo sent a non-member (2880, recorded on hhus, 2026-10-13); a hotel without a
    /// payday turns the block off with the <c>hccenter.activity.enabled</c> client variable.
    /// </summary>
    public int MinutesUntilPayday { get; init; } = 2880;

    /// <summary>How the club centre writes the day a player first joined Habbo Club.</summary>
    public string FirstSubscriptionDateFormat { get; init; } = "yyyy-MM-dd";
}
