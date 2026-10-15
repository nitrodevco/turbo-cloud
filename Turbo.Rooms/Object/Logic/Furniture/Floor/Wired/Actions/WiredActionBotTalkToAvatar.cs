using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// A named bot addresses the selected users: whispering to each (param 0 = 1) or talking
/// once for all (0). Param 1 is the bubble width, param 2 hides the bot's name over the
/// bubble (WIN63-202610121624 <c>actiontypes/_-7V</c>).
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
        [
            new WiredBoolParamRule(true),
            new WiredRangeParamRule(-1, 2, -1),
            new WiredBoolParamRule(false),
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        TextPlaceholdersContext();

    // The placeholders come from the other boxes of the stack, which change without this one
    // being saved: read them again each time the editor opens.
    public override Task<bool> RefreshEditorContextAsync(CancellationToken ct) =>
        Task.FromResult(true);

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => UserAndBotSources();

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, text) = SplitParam();

        if (text.Length == 0 || ResolveBots(ctx, SLOT_BOTS, botName) is not { Count: > 0 } bots)
            return false;

        text = await ctx.FormatTextAsync(text, ct);

        var whisper = GetIntParamOrDefault(0, true);
        var width = GetIntParamOrDefault(1, -1);
        int? bubbleWidth = width < 0 ? null : width;
        var hideName = GetIntParamOrDefault(2, false);
        var players = GetPlayers(TargetUsers(ctx));

        if (players.Count == 0)
            return false;

        foreach (var bot in bots)
        {
            if (!whisper)
            {
                await BotModule.TalkAsync(bot, text, false, bubbleWidth, hideName, ct);

                continue;
            }

            foreach (var player in players)
                await BotModule.WhisperAsync(bot, player, text, bubbleWidth, hideName, ct);
        }

        return true;
    }
}
