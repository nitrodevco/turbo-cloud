using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Grains.Subscriptions;

/// <summary>
/// One player's subscriptions, Habbo Club and Builders Club alike. It is the only way to read or
/// change them; nothing else writes <c>player_subscriptions</c>.
/// </summary>
public interface IPlayerSubscriptionGrain : IGrainWithIntegerKey
{
    public Task<PlayerSubscriptionSnapshot> GetAsync(
        SubscriptionType subscriptionType,
        CancellationToken ct
    );

    /// <summary>The cheap predicate, for callers that only need to know whether it is running.</summary>
    public Task<bool> HasActiveAsync(SubscriptionType subscriptionType, CancellationToken ct);

    /// <summary>
    /// Adds days to a subscription, from now or from its current end, whichever is later, and
    /// tells the player. A Builders Club extension also raises the borrow limit.
    /// </summary>
    public Task ExtendAsync(SubscriptionType subscriptionType, int days, CancellationToken ct);

    /// <summary>A paid catalog extension, recorded separately from administrative grants.</summary>
    public Task ExtendPurchasedAsync(
        SubscriptionType subscriptionType,
        int days,
        CancellationToken ct
    );

    /// <summary>
    /// Sends the Builders Club countdown. Called on login and again whenever it changes. The club
    /// level travels in <c>UserRights</c>, which the permission grain sends; this grain asks it to
    /// when the club changes.
    /// </summary>
    public Task SendStatusAsync(CancellationToken ct);

    /// <summary>Sends the Habbo Club membership detail the club centre and purse read.</summary>
    public Task SendClubInfoAsync(ScrUserInfoResponseType responseType, CancellationToken ct);
}
