using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>A named bot starts (param 0 = 1) or stops (0) following the first selected user.</summary>
[RoomObjectLogic("wf_act_bot_follow_avatar")]
public class WiredActionBotFollowAvatar(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_FOLLOW_AVATAR;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(true)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => UserAndBotSources();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, _) = SplitParam();

        if (ResolveBots(ctx, SLOT_BOTS, botName) is not { Count: > 0 } bots)
            return false;

        var start = GetIntParamOrDefault(0, true);
        var players = start ? GetPlayers(TargetUsers(ctx)) : [];

        if (start && players.Count == 0)
            return false;

        var done = false;

        foreach (var bot in bots)
            done |= start
                ? await BotModule.FollowAsync(bot, players[0].ObjectId, true)
                : await BotModule.FollowAsync(bot, -1, false);

        return done;
    }
}
