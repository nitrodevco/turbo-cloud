using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A recolorable furni (the Builders Club building blocks): map data <c>r</c>, <c>g</c>,
/// <c>b</c> (<see cref="RecolorableData"/>), which the client draws on the asset's
/// <c>recolorable</c> layers. It takes its first colour from the catalogue's purchase parameter
/// and a new one from the recolor window (<c>RecolorFurniture</c>), which only those with room
/// rights see. A recolor may take the other recolorable furni of the room with it, by the
/// window's scope; each only to a colour of its own palette. The room is told once, with one
/// <c>FurniRecolorUpdate</c> entry for all of them.
/// </summary>
[RoomObjectLogic(RecolorableData.LOGIC_NAME)]
public class FurnitureRecolorableLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const string FIRST_STATE = "0";

    protected override StuffDataType _stuffDataType => StuffDataType.MapKey;

    /// <summary>The colour the furni has now; white until it is given one.</summary>
    public int Color =>
        StuffData is IMapStuffData map
            ? RecolorableData.ReadColor(map.Data)
            : RecolorableData.WHITE;

    /// <summary>The colours this furni may take, in palette order.</summary>
    public IReadOnlyList<int> Palette =>
        RecolorableData.GetPalette(
            _roomGrain._roomConfig.RecolorablePaletteToFurni,
            _roomGrain._roomConfig.RecolorablePalettes,
            RecolorableData.ClassName(_ctx.Definition.Name)
        );

    /// <summary>
    /// The colour the catalogue chose (<c>r,g,b</c>, as Flash <c>createStuffData</c> writes it with
    /// state 0). One outside the palette, or no parameter, leaves the furni white.
    /// </summary>
    public override void ApplyCatalogParam(string extraParam)
    {
        if (StuffData is not IMapStuffData map)
            return;

        if (!RecolorableData.TryParseParam(extraParam, out var rgb) || !Palette.Contains(rgb))
            return;

        // A new item's map holds only its state; the official ObjectAdd lists r, b, g, state.
        var entries = RecolorableData.Entries(rgb);

        map.Data.Clear();

        map.Data[RecolorableData.RED] = entries[RecolorableData.RED];
        map.Data[RecolorableData.BLUE] = entries[RecolorableData.BLUE];
        map.Data[RecolorableData.GREEN] = entries[RecolorableData.GREEN];
        map.Data[RecolorableData.STATE] = FIRST_STATE;

        map.MarkDirty();

        PersistStuffData(false);
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not RecolorFurnitureInteraction recolor)
            return false;

        if (!await HasRightsAsync(ctx))
            return Reject(ctx, interaction, "no rights");

        if (!RecolorableData.TryCombine(recolor.Red, recolor.Green, recolor.Blue, out var rgb))
            return Reject(ctx, interaction, "colour out of range");

        if (!Palette.Contains(rgb))
            return Reject(ctx, interaction, "colour not in palette");

        await RecolorAsync(
            Targets(recolor.Scope).Where(x => x.Palette.Contains(rgb)).ToList(),
            rgb
        );

        return true;
    }

    /// <summary>
    /// A wired write of the colour (<c>~recolorable_furni.color.*</c>): only to a colour of the
    /// palette, as the recolor window. False when the colour is not one of them.
    /// </summary>
    public async Task<bool> SetColorAsync(int rgb)
    {
        if (!Palette.Contains(rgb))
            return false;

        if (rgb != Color)
            await RecolorAsync([this], rgb);

        return true;
    }

    /// <summary>
    /// A building block whose furnidata gives a step down per state (<c>customparams</c>, the
    /// recolorable Large Block -0.25 as the plain one) stands lower with each state, as
    /// <see cref="FurnitureMultiHeightLogic"/>; the others keep their height.
    /// </summary>
    public override Altitude GetStackHeight()
    {
        var full = base.GetStackHeight();

        return MultiHeightFurniture.StepOf(_ctx.Definition) is { } step
            ? MultiHeightFurniture.HeightAt(full, GetState(), _ctx.Definition.TotalStates, step)
            : full;
    }

    public override async Task OnStateChangedAsync(CancellationToken ct)
    {
        await base.OnStateChangedAsync(ct);

        if (MultiHeightFurniture.StepOf(_ctx.Definition) is not null)
            await _ctx.UpdateItemAsync();
    }

    /// <summary>Gives the furni the colour, persisted, and tells the room once.</summary>
    private async Task RecolorAsync(List<FurnitureRecolorableLogic> targets, int rgb)
    {
        foreach (var target in targets)
            await target.SetMapDataAsync(RecolorableData.Entries(rgb), false);

        await _ctx.SendComposerToRoomAsync(
            new FurniRecolorUpdateMessageComposer
            {
                Updates =
                [
                    new RecolorUpdateSnapshot
                    {
                        ObjectIds = [.. targets.Select(x => x._ctx.ObjectId)],
                        Red = RecolorableData.Red(rgb),
                        Green = RecolorableData.Green(rgb),
                        Blue = RecolorableData.Blue(rgb),
                    },
                ],
            }
        );
    }

    /// <summary>
    /// This furni and, by the scope, the room's other recolorable furni of its colour, its kind
    /// (the same definition) or both.
    /// </summary>
    private IEnumerable<FurnitureRecolorableLogic> Targets(RecolorScopeType scope)
    {
        if (scope == RecolorScopeType.OnlyThis)
            return [this];

        var color = Color;
        var definitionId = _ctx.Definition.Id;

        return FurniModule
            .Items.OfType<IRoomFloorItem>()
            .Select(x => x.Logic)
            .OfType<FurnitureRecolorableLogic>()
            .Where(x =>
                x == this
                || scope switch
                {
                    RecolorScopeType.SameColor => x.Color == color,
                    RecolorScopeType.SameType => x._ctx.Definition.Id == definitionId,
                    RecolorScopeType.SameTypeAndColor => x._ctx.Definition.Id == definitionId
                        && x.Color == color,
                    _ => false,
                }
            );
    }
}
