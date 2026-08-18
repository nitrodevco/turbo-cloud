using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public async Task OnAchievementProgressAsync(
        PlayerAchievementProgressSnapshot snapshot,
        CancellationToken ct
    )
    {
        if (snapshot is null)
            return;

        await SendComposerAsync(new AchievementEventMessageComposer { Achievement = snapshot });
    }

    public async Task OnAchievementLevelUpAsync(
        AchievementLevelUpSnapshot snapshot,
        CancellationToken ct
    )
    {
        if (snapshot is null)
            return;

        await SendComposerAsync(
            new HabboAchievementNotificationMessageComposer
            {
                Code = snapshot.Code,
                Level = snapshot.Level,
                Progress = snapshot.Progress,
                LevelGoal = snapshot.LevelGoal,
            }
        );

        if (!string.IsNullOrEmpty(snapshot.BadgeCode))
        {
            await SendComposerAsync(
                new BadgeReceivedEventMessageComposer { BadgeCode = snapshot.BadgeCode }
            );
        }
    }
}
