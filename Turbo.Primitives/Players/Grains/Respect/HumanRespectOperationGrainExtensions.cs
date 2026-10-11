using System;
using Orleans;

namespace Turbo.Primitives.Players.Grains.Respect;

public static class HumanRespectOperationGrainExtensions
{
    public static IHumanRespectOperationGrain GetHumanRespectOperationGrain(
        this IGrainFactory grainFactory,
        Guid respectId
    ) => grainFactory.GetGrain<IHumanRespectOperationGrain>(respectId);
}
