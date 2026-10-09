using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Quests.Grains;

/// <summary>A player's progress on the hotel's reward tracks.</summary>
public interface IPlayerRewardTrackGrain : IGrainWithIntegerKey
{
    /// <summary>Sends every track with the player's points, tasks and prizes.</summary>
    Task SendTracksAsync(CancellationToken ct);

    /// <summary>
    /// Counts an action towards every task of that action type. A non-empty
    /// <paramref name="distinctValue"/> counts once per value (a room id for a visit).
    /// </summary>
    Task RecordActionAsync(string actionType, string distinctValue, CancellationToken ct);

    Task ClaimPrizeAsync(string trackId, string prizeId, CancellationToken ct);

    Task PurchasePremiumAsync(string trackId, CancellationToken ct);
}
