using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Notifications;

/// <summary>Best-effort player notices, independent of the action they describe. Offline notices
/// are not queued; login reads current restrictions instead of replaying stale changes.</summary>
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
}
