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
}
