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
/// A named bot talks (param 0 = 0) or shouts (1); param 1 is the bubble width, param 2 hides
/// the bot's name over the bubble (WIN63-202610121624 <c>actiontypes/_-J1x</c>,
/// <c>wiredfurni.params.bot_talk.hide_bot_name</c>).
/// </summary>
[RoomObjectLogic("wf_act_bot_talk")]
public class WiredActionBotTalk(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_TALK;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
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

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, text) = SplitParam();

        if (text.Length == 0 || !TryGetBot(botName, out var bot))
            return false;

        var width = GetIntParamOrDefault(1, -1);

        await BotModule.TalkAsync(
            bot,
            await ctx.FormatTextAsync(text, ct),
            GetIntParamOrDefault(0, false),
            width < 0 ? null : width,
            GetIntParamOrDefault(2, false),
            ct
        );

        return true;
    }
}
