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
/// A named bot addresses the selected users: whispering to each (param 0 = 1), which nobody
/// else hears, or talking to the room with the user's name before the text, "results!: hi"
/// (0). Param 1 is the bubble width, param 2 hides the bot's name over the bubble
/// (WIN63-202610121624 <c>actiontypes/_-7V</c>; official client capture 2026-10-24, two
/// accounts, action.27.R3/R3b 1/1 each). One bot of the name speaks. With several users the
/// talk is said once per user (inferred: recorded with one user).
/// </summary>
[RoomObjectLogic("wf_act_bot_talk_to_avatar")]
public class WiredActionBotTalkToAvatar(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_TALK_DIRECT_TO_AVTR;

    protected override bool ActsOnOneBot => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            // A fresh box talks (0) rather than whispers on the official server (defaults
            // [0,-1,0], 2026-10-20T17-39-35-Open-wait; AS3 actiontypes/_-A1j.as:86 whisper = 1).
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(-1, 2, -1),
            new WiredBoolParamRule(false),
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        TextPlaceholdersContext();

    // The placeholders come from the other boxes of the stack, which change without this one
    // being saved: read them again each time the editor opens.
    public override Task<bool> RefreshEditorContextAsync(CancellationToken ct) =>
        Task.FromResult(true);

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [BotSources, UserSources];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, text) = SplitParam();

        if (text.Length == 0 || ResolveBots(ctx, SLOT_BOTS, botName) is not { Count: > 0 } bots)
            return false;

        text = await ctx.FormatTextAsync(text, ct);

        var whisper = GetIntParamOrDefault(0, false);
        var width = GetIntParamOrDefault(1, -1);
        int? bubbleWidth = width < 0 ? null : width;
        var hideName = GetIntParamOrDefault(2, false);
        var players = GetPlayers(TargetUsers(ctx));

        if (players.Count == 0)
            return false;

        foreach (var bot in bots)
        foreach (var player in players)
        {
            if (whisper)
                await BotModule.WhisperAsync(bot, player, text, bubbleWidth, hideName, ct);
            else
                await BotModule.TalkAsync(
                    bot,
                    $"{player.Name}: {text}",
                    false,
                    bubbleWidth,
                    hideName,
                    ct
                );
        }

        return true;
    }
}
