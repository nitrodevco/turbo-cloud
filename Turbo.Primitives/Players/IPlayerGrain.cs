using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Players;

namespace Turbo.Primitives.Players;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task<long> GetPlayerIdAsync();

    public ValueTask<PlayerSnapshot> GetSnapshotAsync(CancellationToken ct);
}
