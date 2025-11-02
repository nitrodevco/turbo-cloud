using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Snapshots.Players;

namespace Turbo.Players.Abstractions;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task<long> GetPlayerIdAsync();

    public ValueTask<PlayerSnapshot> GetSnapshotAsync(CancellationToken ct);
}
