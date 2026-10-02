using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Achievements.Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

[Command(
    "achievements",
    Description = "Inspect, advance, reconcile or retry permanent achievement awards",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.ACHIEVEMENTS)]
public sealed class AchievementAdminCommand(IGrainFactory grains)
    : IOperatorCommand<AchievementAdminArguments>
{
    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            ["done"] = "Achievement operation completed.",
            ["invalid"] = "Supply a stable operation id and an audit reason.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        AchievementAdminArguments arguments,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(arguments.OperationId)
            || string.IsNullOrWhiteSpace(arguments.Reason.Text)
        )
            return CommandResult.Fail("invalid");
        var selection = await ctx.ResolveAsync(arguments.Who, ct);
        if (selection.Failure is { } failure)
            return failure;
        var player = selection.Players[0];
        var achievements = grains.GetPlayerAchievementGrain(player.Id);
        switch (arguments.Action)
        {
            case AchievementAdminAction.Inspect:
                var catalog = await achievements.GetAchievementsAsync(ct);
                await ctx.Executor.NoticeAsync(
                    catalog
                        .Select(x =>
                            $"{x.AchievementId}: target {x.Level}/{x.LevelCount}, progress {x.CurrentPointsTotal}, state {x.State}"
                        )
                        .ToArray(),
                    ct
                );
                var pending = await achievements.GetPendingAwardsAsync(ct);
                if (pending.Length > 0)
                    await ctx.Executor.NoticeAsync(
                        pending
                            .Select(x =>
                                $"{x.AwardKey}: delivered rewards {x.DeliveredRewards}; {x.BlockedReason ?? "pending"}"
                            )
                            .ToArray(),
                        ct
                    );
                break;
            case AchievementAdminAction.Advance:
                await achievements.AdvanceAsync(
                    arguments.AchievementId,
                    arguments.Progress,
                    ctx.Executor.Name,
                    arguments.Reason.Text,
                    arguments.OperationId,
                    ct
                );
                break;
            case AchievementAdminAction.Reconcile:
                await achievements.AdministerAsync(
                    false,
                    ctx.Executor.Name,
                    arguments.Reason.Text,
                    arguments.OperationId,
                    ct
                );
                break;
            case AchievementAdminAction.Retry:
                await achievements.AdministerAsync(
                    true,
                    ctx.Executor.Name,
                    arguments.Reason.Text,
                    arguments.OperationId,
                    ct
                );
                break;
            default:
                throw new InvalidOperationException("Unknown achievement administration action.");
        }
        return CommandResult.Done("done");
    }
}
