using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Turbo.Primitives.Players.Grains;

/// <summary>
/// Hotel-wide player id and name lookups. Every method is interleaved: this is one grain on
/// everybody's hot path, and a cache miss that queries the database must not hold up the
/// lookups queued behind it. The implementation touches its cache only between awaits.
/// </summary>
public interface IPlayerDirectoryGrain : IGrainWithStringKey
{
    [AlwaysInterleave]
    public Task<string> GetPlayerNameAsync(PlayerId playerId, CancellationToken ct);

    [AlwaysInterleave]
    public Task<ImmutableDictionary<PlayerId, string>> GetPlayerNamesAsync(
        List<PlayerId> playerIds,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task<PlayerId?> GetPlayerIdAsync(string userName, CancellationToken ct);

    /// <summary>
    /// At most <paramref name="limit"/> player names that begin with <paramref name="prefix"/>,
    /// ignoring case, sorted: for a client completing a name as it is typed.
    /// </summary>
    [AlwaysInterleave]
    public Task<ImmutableArray<string>> SearchNamesAsync(
        string prefix,
        int limit,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task SetPlayerNameAsync(PlayerId playerId, string name, CancellationToken ct);
}
