using Orleans;

namespace Turbo.Primitives.Players.Grains.Respect;

public static class HumanRespectOperationGrainExtensions
{
    public static IHumanRespectOperationGrain GetHumanRespectOperationGrain(
        this IGrainFactory grainFactory,
        string operationId
    ) => grainFactory.GetGrain<IHumanRespectOperationGrain>(operationId);
}
