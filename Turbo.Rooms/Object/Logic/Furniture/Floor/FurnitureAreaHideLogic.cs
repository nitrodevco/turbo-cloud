using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Wired.Variables.Furniture.Smart;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// An area hider (<c>conf_area_hide</c>, "Room Area Hider"): int data <c>[state, rootX, rootY,
/// width, length, invisibility, wallItems, invert]</c> as the client's area-hide logic reads it.
/// The client does the hiding (a hole in the floor, Flash <c>RoomEngine.updateAreaHide</c>); the
/// server keeps the settings, toggles them, announces both, and sends the areas in force to
/// whoever walks in.
/// </summary>
[RoomObjectLogic("area_hide")]
public class FurnitureAreaHideLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx),
        IIndexedValueLogic
{
    private const int OFF = 0;
    private const int ON = 1;
    private const int STATE_INDEX = 0;
    private const int ROOT_X_INDEX = 1;
    private const int ROOT_Y_INDEX = 2;
    private const int WIDTH_INDEX = 3;
    private const int LENGTH_INDEX = 4;
    private const int INVISIBILITY_INDEX = 5;
    private const int WALL_ITEMS_INDEX = 6;
    private const int INVERT_INDEX = 7;

    // Picked up: it hides nothing any more, though it is still on.
    private bool _detached;

    protected override StuffDataType _stuffDataType => StuffDataType.NumberKey;

    /// <summary>One of the int data values (<c>~area_hide.*</c> reads them).</summary>
    public int ValueAt(int index) =>
        StuffData is INumberStuffData numbers ? numbers.ValueAt(index) : 0;

    /// <summary>Changes one int data value, as a smart variable write does, and announces it.</summary>
    public async Task SetValueAtAsync(int index, int value)
    {
        if (StuffData is not INumberStuffData numbers)
            return;

        var values = Enumerable.Range(0, INVERT_INDEX + 1).Select(numbers.ValueAt).ToArray();

        values[index] = value;

        await ApplyAsync(values);
    }

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (StuffData is not INumberStuffData numbers || !await HasRightsAsync(ctx))
            return;

        var next = numbers.ValueAt(STATE_INDEX) == ON ? OFF : ON;

        await ApplyAsync([
            next,
            numbers.ValueAt(ROOT_X_INDEX),
            numbers.ValueAt(ROOT_Y_INDEX),
            numbers.ValueAt(WIDTH_INDEX),
            numbers.ValueAt(LENGTH_INDEX),
            numbers.ValueAt(INVISIBILITY_INDEX),
            numbers.ValueAt(WALL_ITEMS_INDEX),
            numbers.ValueAt(INVERT_INDEX),
        ]);
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (
            interaction is not SetAreaHideInteraction area
            || StuffData is not INumberStuffData numbers
        )
            return false;

        if (!await HasRightsAsync(ctx))
            return Reject(ctx, interaction, "no rights");

        var maxSize = _roomGrain._roomConfig.AreaHideMaxSize;

        if (
            area.Width < 1
            || area.Length < 1
            || area.Width > maxSize
            || area.Length > maxSize
            || !MapModule.InBounds(area.RootX, area.RootY)
        )
            return Reject(ctx, interaction, "area out of range");

        await ApplyAsync([
            numbers.ValueAt(STATE_INDEX),
            area.RootX,
            area.RootY,
            area.Width,
            area.Length,
            area.Invisibility ? 1 : 0,
            area.WallItems ? 1 : 0,
            area.Invert ? 1 : 0,
        ]);

        return true;
    }

    /// <summary>
    /// The area this hider hides right now, or null while it is off: what a player who walks in
    /// is sent with the floor map, as Flash's <c>FloorHeightMapMessageParser</c> reads it.
    /// </summary>
    public AreaHideDataSnapshot? GetActiveArea()
    {
        var area = ToSnapshot();

        return !_detached && area is { On: true } ? area : null;
    }

    /// <summary>Picked up while on: the clients fill the hole again and see what it hid.</summary>
    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);

        if (GetActiveArea() is not { } area)
            return;

        var hiddenBefore = FurniModule.GetHiddenItemIds();

        _detached = true;

        await _ctx.SendComposerToRoomAsync(
            new AreaHideMessageComposer { AreaHideData = area with { On = false } }
        );
        await FurniModule.AnnounceHiddenItemsChangedAsync(hiddenBefore, ct);
    }

    private AreaHideDataSnapshot? ToSnapshot() =>
        StuffData is INumberStuffData numbers
            ? new AreaHideDataSnapshot
            {
                FurniId = _ctx.ObjectId,
                On = numbers.ValueAt(STATE_INDEX) == ON,
                RootX = numbers.ValueAt(ROOT_X_INDEX),
                RootY = numbers.ValueAt(ROOT_Y_INDEX),
                Width = numbers.ValueAt(WIDTH_INDEX),
                Length = numbers.ValueAt(LENGTH_INDEX),
                Invert = numbers.ValueAt(INVERT_INDEX) == 1,
            }
            : null;

    /// <summary>
    /// Whether this hider, switched on, hides the item: a floor item standing in its area, a wall
    /// item there when it hides wall items, or with "invert" whatever stands outside it. The hider
    /// itself always shows, or nobody could switch it off again. An item's own tile decides, not
    /// its whole footprint (inference: the client's area is a set of floor tiles).
    /// </summary>
    public bool Hides(IRoomItem item)
    {
        if (item.ObjectId == _ctx.ObjectId || GetActiveArea() is not { } area)
            return false;

        if (item is IRoomWallItem && ValueAt(WALL_ITEMS_INDEX) != 1)
            return false;

        var inside =
            item.X >= area.RootX
            && item.X < area.RootX + area.Width
            && item.Y >= area.RootY
            && item.Y < area.RootY + area.Length;

        return inside != area.Invert;
    }

    /// <summary>
    /// Stores new settings and tells the room: the area itself, then the furni it now hides or
    /// shows again (Habbo: "Fixed floor furni not appearing after revealed by area hider").
    /// </summary>
    private async Task ApplyAsync(int[] values)
    {
        var hiddenBefore = FurniModule.GetHiddenItemIds();

        await SetNumberDataAsync(values);

        if (ToSnapshot() is { } area)
            await _ctx.SendComposerToRoomAsync(new AreaHideMessageComposer { AreaHideData = area });

        await FurniModule.AnnounceHiddenItemsChangedAsync(hiddenBefore, CancellationToken.None);
    }
}
