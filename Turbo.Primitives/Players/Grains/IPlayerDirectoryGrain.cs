using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerDirectoryGrain : IGrainWithStringKey
{
    public Task<string> GetPlayerNameAsync(long playerId, CancellationToken ct);
    public Task<ImmutableDictionary<long, string>> GetPlayerNamesAsync(
        List<long> playerIds,
        CancellationToken ct
    );
    public Task SetPlayerNameAsync(long playerId, string name, CancellationToken ct);
    public Task InvalidatePlayerNameAsync(long playerId, CancellationToken ct);
}
