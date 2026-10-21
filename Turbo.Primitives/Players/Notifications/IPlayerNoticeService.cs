using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Notifications;

/// <summary>Best-effort player notices, independent of the action they describe. Offline notices
/// are not queued, except the hotel's persistent notifications (<see cref="SendPersistentAsync"/>);
/// login reads current restrictions instead of replaying stale changes.</summary>
public interface IPlayerNoticeService
{
    Task<PlayerNoticeDelivery> SendCurrencyRewardAsync(
        PlayerId playerId,
        long amount,
        CurrencyTypeSnapshot currency,
        CancellationToken ct
    );

    Task<PlayerNoticeDelivery> SendAsync(
        PlayerId playerId,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    );

    /// <summary>
    /// A NotificationDialog of a <c>"delivery":"PERSISTENT"</c> type: sent now when the player is
    /// online, otherwise kept and sent at their next login (<see cref="DeliverPersistentAsync"/>),
    /// the newest few only. Offline means kept here.
    /// </summary>
    Task<PlayerNoticeDelivery> SendPersistentAsync(
        PlayerId playerId,
        string notificationType,
        ImmutableDictionary<string, string> parameters,
        CancellationToken ct
    );

    /// <summary>Sends a logging-in player what <see cref="SendPersistentAsync"/> kept, oldest first.</summary>
    Task DeliverPersistentAsync(PlayerId playerId, CancellationToken ct);
}
