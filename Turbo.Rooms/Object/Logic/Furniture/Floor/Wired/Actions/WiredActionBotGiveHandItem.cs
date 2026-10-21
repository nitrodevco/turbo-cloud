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

/// <summary>
/// Hands the selected users the hand item in param 0. When a bot is named it must be in the
/// room; the item still lands in the users hands.
/// </summary>
[RoomObjectLogic("wf_act_bot_give_handitem")]
public class WiredActionBotGiveHandItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_GIVE_HAND_ITEM;

    protected override bool IsBotOptional => true;

    public override List<IWiredParamRule> GetIntParamRules() => [WiredRules.HandItem(WiredConfig)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => UserAndBotSources();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, _) = SplitParam();

        if (
            IsBotByName(SLOT_BOTS)
                ? !TryResolveBot(botName, out _)
                : ResolveBots(ctx, SLOT_BOTS, botName).Count == 0
        )
            return false;

        var handItemId = GetIntParamOrDefault(0, 0);
        var players = GetPlayers(TargetUsers(ctx));

        foreach (var player in players)
            await AvatarModule.SetHandItemAsync(player, handItemId, ct);

        return players.Count > 0;
    }
}
