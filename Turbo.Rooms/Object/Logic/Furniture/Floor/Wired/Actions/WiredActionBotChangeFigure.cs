using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Dresses every bot of the name in the figure string after the tab in the string param; the
/// bot keeps its gender. The figure is stored as the bot may wear it: each colour the figure
/// data does not offer outside the club swapped for the palette's first, every piece kept, for
/// whoever it was made (official client capture 2026-10-24, action.26.R3: a female figure saved
/// on a male bot came back from Open with hr-515-33 as hr-515-40 and ch-635-70 as ch-635-1408,
/// UserChange carried that figure, gender "m"; which colours count is inferred from a saver
/// without club).
/// </summary>
[RoomObjectLogic("wf_act_bot_clothes")]
public class WiredActionBotChangeFigure(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    IFigureDataProvider figureData
) : FurnitureWiredBotActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.BOT_CHANGE_FIGURE;

    protected override async Task<string> StoredStringParamAsync(
        string stringParam,
        CancellationToken ct
    )
    {
        var index = stringParam.IndexOf(FIELD_SEPARATOR);

        if (index < 0)
            return stringParam;

        var data = await figureData.GetAsync(ct);
        var figure = FigureRules.FitColors(data, stringParam[(index + 1)..], 0);

        return $"{stringParam[..(index + 1)]}{figure}";
    }

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var (botName, figure) = SplitParam();

        if (
            !FigureString.IsWellFormed(figure)
            || ResolveBots(ctx, SLOT_BOTS, botName) is not { Count: > 0 } bots
        )
            return false;

        foreach (var bot in bots)
            await BotModule.SetFigureAsync(bot, figure, bot.Gender, ct);

        return true;
    }
}
