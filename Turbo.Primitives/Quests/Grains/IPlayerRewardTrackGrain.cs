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
    /// Sends the tracks again with <c>reload</c> set, after staff changed them: the client
    /// rebuilds its track window and says the track was updated if it is open.
    /// </summary>
    Task ReloadTracksAsync(CancellationToken ct);

    /// <summary>
    /// Counts an action towards every task of that action type. A non-empty
    /// <paramref name="distinctValue"/> counts once per value (a room id for a visit).
    /// </summary>
    Task RecordActionAsync(string actionType, string distinctValue, CancellationToken ct);

    /// <summary>
    /// Moves one task's count to <paramref name="score"/>, or on by it when
    /// <paramref name="add"/>, as the wired Progress Reward Track does: forward only, up to the
    /// task's last level, each level passed giving its points. False when nothing moved.
    /// </summary>
    Task<bool> ProgressTaskAsync(
        string trackId,
        string taskId,
        int score,
        bool add,
        CancellationToken ct
    );

    /// <summary>
    /// Takes the player back to the start of a track (counts, points and claimed prizes; a
    /// bought premium stays) and sends the tracks again to reload. False for an unknown track
    /// or one the player has no progress on.
    /// </summary>
    Task<bool> ResetTrackAsync(string trackId, CancellationToken ct);

    Task ClaimPrizeAsync(string trackId, string prizeId, CancellationToken ct);

    Task PurchasePremiumAsync(string trackId, CancellationToken ct);
}
