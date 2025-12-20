using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Players;

public sealed class PlayerService(IGrainFactory grainFactory) : IPlayerService
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public IPlayerGrain GetPlayerGrain(PlayerId playerId) =>
        _grainFactory.GetGrain<IPlayerGrain>(playerId);
}
