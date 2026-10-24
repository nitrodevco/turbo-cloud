using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// A bot takes the hand item in param 0 to the selected user: one bot of the name carries it,
/// walks next to the user and hands it over (<see cref="RoomBotModule.GiveHandItemAsync"/>).
/// With no bot named the users get the item directly (official client capture 2026-10-24,
/// action.24.B1/R1/R3). A bot hands one item, so it goes to the first user (inferred).
/// </summary>
[RoomObjectLogic("wf_act_bot_give_handitem")]
public class WiredActionBotGiveHandItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_GIVE_HAND_ITEM;

    protected override bool ActsOnOneBot => true;

    public override List<IWiredParamRule> GetIntParamRules() => [WiredRules.HandItem(WiredConfig)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [BotSources, UserSources];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, _) = SplitParam();
        var handItemId = GetIntParamOrDefault(0, 0);
        var players = GetPlayers(TargetUsers(ctx));

        if (players.Count == 0)
            return false;

        // The editor's "bot.usage" checkbox off sends an empty name: no bot takes part.
        if (IsBotByName(SLOT_BOTS) && botName.Length == 0)
        {
            foreach (var player in players)
                await AvatarModule.SetHandItemAsync(player, handItemId, ct);

            return true;
        }

        if (ResolveBots(ctx, SLOT_BOTS, botName) is not [var bot, ..])
            return false;

        await BotModule.GiveHandItemAsync(bot, players[0], handItemId, ct);

        return true;
    }
}
