using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Plugins;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

/// <summary>
/// What the hosts of plugin wired boxes (<see cref="IPluginWiredHost"/>) share: a plugin box's
/// declaration (<see cref="WiredBox"/>) read as this base's rules, and the context it runs with.
/// The checks, flashes, limits and stack rules stay this base's and its kind's, unchanged.
/// </summary>
public abstract partial class FurnitureWiredLogic
{
    /// <summary>The box's int params as this base's rules, in order.</summary>
    private protected static List<IWiredParamRule> PluginRules(WiredBox box) =>
        [
            .. box.Params.Select(param =>
                param switch
                {
                    { IsBool: true } => (IWiredParamRule)new WiredBoolParamRule(param.Default != 0),
                    { Values: { } values } => new WiredSetParamRule(param.Default, values),
                    _ => new WiredRangeParamRule(param.Min, param.Max, param.Default),
                }
            ),
        ];

    private protected static List<WiredFurniSourceType[]> PluginFurniSources(WiredBox box) =>
        box.ActsOnFurni ? [WiredSources.Furni] : [];

    private protected static List<WiredPlayerSourceType[]> PluginPlayerSources(WiredBox box) =>
        box.ActsOnUsers ? [WiredSources.Users] : [];

    /// <summary>The box's text limit, never past the hotel's.</summary>
    private protected int PluginTextLimit(WiredBox box) =>
        Math.Clamp(
            box.TextMaxLength ?? WiredConfig.StringParamMaxLength,
            0,
            WiredConfig.StringParamMaxLength
        );

    /// <summary>The box's furni limit, never past the hotel's.</summary>
    private protected int PluginFurniLimit(WiredBox box) =>
        Math.Clamp(
            box.FurniLimit ?? WiredConfig.SelectedItemsLimit,
            0,
            WiredConfig.SelectedItemsLimit
        );

    /// <summary>The picks the base keeps, cut to the box's own furni limit.</summary>
    private protected bool PluginValidStuffIds(WiredBox box, List<int> stuffIds)
    {
        var limit = PluginFurniLimit(box);

        if (stuffIds.Count > limit)
            stuffIds.RemoveRange(limit, stuffIds.Count - limit);

        return true;
    }

    /// <summary>
    /// The box as saved, for a plugin: a param is read as the int it is stored as (a switch as 0 or
    /// 1), which the base's typed accessor would refuse for a switch.
    /// </summary>
    private protected IWiredBoxConfig PluginConfig(WiredBox box) =>
        new PluginWiredBoxConfig(GetStringParam(), ParamOf(box), GetStuffIds());

    private Func<int, int> ParamOf(WiredBox box) =>
        index =>
            _wiredData is { } data && index >= 0 && index < data.IntParams.Count
                ? data.IntParams[index]
            : index >= 0 && index < box.Params.Count ? box.Params[index].Default
            : 0;

    /// <summary>The context a plugin box runs with in <paramref name="ctx"/>'s execution.</summary>
    private protected PluginWiredRunContext PluginContext(
        WiredBox box,
        IWiredContext ctx,
        IWiredExecutionContext? execution
    )
    {
        var selection =
            box.ActsOnUsers || box.ActsOnFurni ? ctx.GetSelection(this) : new WiredSelectionSet();

        return new PluginWiredRunContext(
            _ctx.RoomId,
            PluginConfig(box),
            box.ActsOnUsers ? [.. GetAvatars(selection).Select(PluginRoomViews.User)] : [],
            box.ActsOnFurni ? [.. GetFloorItems(selection).Select(PluginRoomViews.Furni)] : [],
            () => [.. AvatarModule.Avatars.Select(PluginRoomViews.User)],
            () =>
                [.. State.ItemsById.Values.OfType<IRoomFloorItem>().Select(PluginRoomViews.Furni)],
            execution,
            SayForPluginAsync
        );
    }

    private Task<bool> SayForPluginAsync(
        RoomUser user,
        string text,
        RoomSpeech speech,
        CancellationToken ct
    ) => PluginRoomTurn.SayAsync(AvatarModule, ChatSystem, user, text, speech, ct);
}
