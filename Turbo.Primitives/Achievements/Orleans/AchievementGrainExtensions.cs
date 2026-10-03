using Orleans;
using Turbo.Primitives.Achievements.Grains;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Achievements.Orleans;

public static class AchievementGrainExtensions
{
    public static IPlayerAchievementGrain GetPlayerAchievementGrain(
        this IGrainFactory factory,
        PlayerId playerId
    ) => factory.GetGrain<IPlayerAchievementGrain>(playerId.Value);
}
