using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Achievements.Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Progresses one of the room's wired achievements for the selected users (Flash
/// <c>actiontypes.ProgressAchievement</c>, <c>ActionTypeCodes.PROGRESS_ACHIEVEMENT</c>).
/// <para>
/// String param: the achievement, one of the names the room's Achievement Enabler add-ons enable
/// (the editor's dropdown lists <c>WiredEnvironment</c>'s achievements); any other is refused
/// with <c>wiredfurni.error.achievement_not_allowed</c>. The name has no <c>ACH_WF_</c> prefix:
/// it is the achievement whose badge is <c>ACH_WF_&lt;name&gt;&lt;level&gt;</c>, as the
/// achievements window's "wired_games" category matches <c>WF_&lt;name&gt;</c> against the same
/// list (<c>AchievementController.achievementIsVisible</c>).
/// </para>
/// <para>
/// Int params, as the editor writes them: the mode (1 "Add to existing progress", 0 "Set
/// progress"), then the value-or-variable section (0 the number, 1 a variable), the number, and
/// the variable's target. User slot 0 is who is progressed; furni slot 0 and user slot 1 are the
/// merged source the variable is read on (<c>mergedSelections [[0, 1]]</c>).
/// </para>
/// Progress only moves forward here (the hotel's achievements are advanced, never taken back),
/// so "Set progress" below the user's progress leaves it (inferred: no official word on it).
/// </summary>
[RoomObjectLogic("wf_act_progress_ach")]
public class WiredActionProgressAchievement(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_MODE = 0;
    private const int PARAM_IS_VARIABLE = 1;
    private const int PARAM_VALUE = 2;
    private const int PARAM_TARGET = 3;

    private const int SLOT_REFERENCE_FURNI = 0;
    private const int SLOT_USERS = 0;
    private const int SLOT_REFERENCE_USERS = 1;
    private const int VARIABLE_VALUE = 0;

    // Flash AchievementData.code: the badge id without "ACH_" and its level digits.
    private const string BADGE_PREFIX = "ACH_";
    private const string WIRED_ACHIEVEMENT_PREFIX = "WF_";

    public override int WiredCode => (int)WiredActionType.PROGRESS_ACHIEVEMENT;

    protected override bool HasPositionalVariableIds => true;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, int.MaxValue, 0),
            WiredRules.VariableTarget(WiredVariableTargetType.User),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        AllVariablesContext();

    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (!IsEnabled(update.StringParam?.Trim() ?? string.Empty))
            throw new WiredSaveRefusedException(WiredSaveErrors.ACHIEVEMENT_NOT_ALLOWED);

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var name = GetStringParam();

        // An enabler taken away after the save takes the achievement with it.
        if (!IsEnabled(name))
            return false;

        var players = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_USERS));

        if (players.Count == 0)
            return false;

        var amount = ResolveAmount(ctx);
        var adds = GetIntParamOrDefault(PARAM_MODE, false);
        var code = WIRED_ACHIEVEMENT_PREFIX + name;
        var progressed = false;

        foreach (var player in players)
        {
            try
            {
                var grain = _grainFactory.GetPlayerAchievementGrain(player.PlayerId);
                var achievement = (await grain.GetAchievementsAsync(ct)).FirstOrDefault(x =>
                    CodeOf(x.BadgeId) == code
                );

                if (achievement is null)
                    continue;

                long current = achievement.CurrentPointsTotal;
                var target = adds ? current + amount : amount;

                if (target <= current)
                    continue;

                await grain.AdvanceAsync(
                    achievement.AchievementId,
                    target,
                    nameof(WiredActionProgressAchievement),
                    $"wired {_ctx.ObjectId} in room {_roomGrain.RoomId}",
                    $"wired:{_roomGrain.RoomId}:{_ctx.ObjectId}:{player.PlayerId.Value}:{Guid.NewGuid():N}",
                    ct
                );

                progressed = true;
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Wired achievement {Code} could not be progressed for player {PlayerId} in room {RoomId}",
                    code,
                    player.PlayerId,
                    _roomGrain.RoomId
                );
            }
        }

        return progressed;
    }

    private bool IsEnabled(string name) =>
        name.Length > 0 && WiredSystem.EnabledAchievements.Contains(name);

    private long ResolveAmount(IWiredExecutionContext ctx)
    {
        long value = GetIntParamOrDefault(PARAM_VALUE, 0);

        if (!GetIntParamOrDefault(PARAM_IS_VARIABLE, false))
            return value;

        var reference = new WiredSelectionSet();

        reference.UnionWith(WiredSlotSelection.ForSlot(this, ctx, SLOT_REFERENCE_FURNI));
        reference.UnionWith(WiredSlotSelection.ForUserSlot(this, ctx, SLOT_REFERENCE_USERS));

        return TryReadVariableOperand(VARIABLE_VALUE, PARAM_TARGET, reference, out value)
            ? Math.Max(0, value)
            : 0;
    }

    /// <summary>Flash <c>AchievementData.code</c>: "ACH_WF_Lap3" is "WF_Lap".</summary>
    public static string CodeOf(string badgeId)
    {
        var code = badgeId.StartsWith(BADGE_PREFIX, StringComparison.Ordinal)
            ? badgeId[BADGE_PREFIX.Length..]
            : badgeId;

        return code.TrimEnd("0123456789".ToCharArray());
    }
}
