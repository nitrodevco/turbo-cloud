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
/// A named bot addresses the selected users: whispering to each (param 0 = 1) or talking
/// once for all (0). Param 1 is the bubble width.
/// </summary>
[RoomObjectLogic("wf_act_bot_talk_to_avatar")]
public class WiredActionBotTalkToAvatar(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_TALK_DIRECT_TO_AVTR;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredBoolParamRule(true), new WiredRangeParamRule(-1, 2, -1)];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
            [WiredPlayerSourceType.BotByName],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, text) = SplitParam();

        if (text.Length == 0 || !TryGetBot(botName, out var bot))
            return false;

        text = await ctx.FormatTextAsync(text, ct);

        var whisper = GetIntParamOrDefault(0, true);
        var width = GetIntParamOrDefault(1, -1);
        int? bubbleWidth = width < 0 ? null : width;
        var players = GetPlayers(ctx.GetSelection(this));

        if (players.Count == 0)
            return false;

        if (!whisper)
        {
            await _roomGrain.BotModule.TalkAsync(bot, text, false, bubbleWidth, ct);

            return true;
        }

        foreach (var player in players)
            await _roomGrain.BotModule.WhisperAsync(bot, player, text, bubbleWidth, ct);

        return true;
    }
}
