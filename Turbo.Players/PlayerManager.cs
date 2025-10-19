using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Grains;

namespace Turbo.Players;

public sealed class PlayerManager(IGrainFactory grainFactory)
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task<IPlayerGrain> GetPlayerGrainAsync(long playerId)
    {
        var grain = _grainFactory.GetGrain<IPlayerGrain>(playerId);

        return await Task.FromResult(grain).ConfigureAwait(false);
    }
}
