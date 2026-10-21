using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Quests.RewardTracks;

/// <summary>
/// The hotel's reward tracks, kept in the database (<c>reward_tracks</c> and the task, level and
/// prize tables) and cached here: what players are sent, count and claim against. A change saved
/// through the store is in the cache when the call returns, so no restart is needed.
/// </summary>
public interface IRewardTrackStore
{
    /// <summary>Every track, live or not, in the order players are sent them.</summary>
    ImmutableArray<RewardTrackDefinition> Tracks { get; }

    /// <summary>The tracks players get at <paramref name="utcNow"/>: on, and within their dates.</summary>
    ImmutableArray<RewardTrackDefinition> LiveAt(DateTime utcNow);

    /// <summary>Whether a task of an enabled track counts <paramref name="actionType"/> (any case).</summary>
    bool Counts(string actionType);

    /// <summary>Reads the tracks from the database again.</summary>
    Task ReloadAsync(CancellationToken ct);

    /// <summary>
    /// Saves each track by its id, adding it or replacing what it was (its tasks, levels and
    /// prizes with it), in one transaction, in the order given after the tracks already there.
    /// Throws <see cref="ArgumentException"/> with <see cref="RewardTrackRules.Problem"/> when the
    /// tracks would be invalid, and saves none.
    /// </summary>
    Task SaveAsync(IReadOnlyList<RewardTrackDefinition> tracks, CancellationToken ct);

    /// <summary>Removes a track. Players' progress on it stays, for a track of the same id.</summary>
    Task<bool> DeleteAsync(string trackId, CancellationToken ct);

    /// <summary>
    /// Puts the tracks in the order players are sent them (the Progression menu's order of its
    /// track items): the ones named, in that order, then any other in the order it had. Throws
    /// <see cref="ArgumentException"/> for an id named twice or no track has, and changes nothing.
    /// </summary>
    Task ReorderAsync(IReadOnlyList<string> trackIds, CancellationToken ct);
}
