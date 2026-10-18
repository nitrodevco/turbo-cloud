using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Quests.RewardTracks;

namespace Turbo.Tests.Support;

/// <summary>Reward tracks held in a list, read each time, for tests that change them as they go.</summary>
internal sealed class ListRewardTrackStore(Func<IReadOnlyList<RewardTrackDefinition>> tracks)
    : IRewardTrackStore
{
    public ImmutableArray<RewardTrackDefinition> Tracks => [.. tracks()];

    public ImmutableArray<RewardTrackDefinition> LiveAt(DateTime utcNow) =>
        [.. tracks().Where(x => x.IsLiveAt(utcNow))];

    public bool Counts(string actionType) =>
        tracks()
            .Where(x => x.Enabled)
            .SelectMany(x => x.Tasks)
            .Any(x => string.Equals(x.ActionType, actionType, StringComparison.OrdinalIgnoreCase));

    public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;

    public Task SaveAsync(IReadOnlyList<RewardTrackDefinition> saved, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<bool> DeleteAsync(string trackId, CancellationToken ct) =>
        throw new NotSupportedException();
}
