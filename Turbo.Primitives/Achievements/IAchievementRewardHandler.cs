using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Achievements;

/// <summary>Implementations must durably deduplicate awardKey and reject payload reinterpretation.</summary>
public interface IAchievementRewardHandler
{
    string Key { get; }
    int Version { get; }
    Task DeliverAsync(
        PlayerId playerId,
        string awardKey,
        AchievementReward reward,
        CancellationToken ct
    );
}
