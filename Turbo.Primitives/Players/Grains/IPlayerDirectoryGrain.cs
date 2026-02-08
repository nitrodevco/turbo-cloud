using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerDirectoryGrain : IGrainWithStringKey
{
    public Task<string> GetPlayerNameAsync(PlayerId playerId, CancellationToken ct);
    public Task<ImmutableDictionary<PlayerId, string>> GetPlayerNamesAsync(
        List<PlayerId> playerIds,
        CancellationToken ct
    );
    public Task<PlayerId?> GetPlayerIdAsync(string userName, CancellationToken ct);
    public Task SetPlayerNameAsync(PlayerId playerId, string name, CancellationToken ct);
}
