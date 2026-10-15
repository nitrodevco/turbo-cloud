using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// The "override height" effect (Flash <c>ActionTypeCodes.OVERRIDE_HEIGHT</c> 53). Only its
/// editor is known (<c>actiontypes._-O18</c>): a type radio (0 or 1; type 1 disables the
/// height slider) and a height slider from 0 to 8000 that echoes the raw number, written as
/// [height, type]. The box is not in Habbo's furnidata and its labels are not in the hotel texts,
/// so neither what it changes the height of nor the slider's unit is known. It saves and keeps
/// its settings so the editor round-trips, and does nothing when run until there is an official
/// account of what it does.
/// <para>
/// The classname is this server's (no official furni exists yet); a hotel may also point a
/// definition's logic column at it.
/// </para>
/// </summary>
[RoomObjectLogic("wf_act_override_height")]
public class WiredActionOverrideHeight(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    // The editor's slider (createSliderSection(..., 0, 8000, 1)).
    private const int MAX_HEIGHT = 8000;

    public override int WiredCode => (int)WiredActionType.OVERRIDE_HEIGHT;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredRangeParamRule(0, MAX_HEIGHT, 0), new WiredBoolParamRule(false)];

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct) =>
        Task.FromResult(false);
}
