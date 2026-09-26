using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// An area hider: int data <c>[state, rootX, rootY, width, length, invisibility, wallItems,
/// invert]</c> as the client's area-hide logic reads it. The client does the hiding; the server
/// keeps the settings, toggles them, and announces both.
/// </summary>
[RoomObjectLogic("area_hide")]
public class FurnitureAreaHideLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
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

    protected override StuffDataType _stuffDataType => StuffDataType.NumberKey;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (StuffData is not INumberStuffData numbers || !await HasRightsAsync(ctx))
            return;

        var next = numbers.ValueAt(STATE_INDEX) == ON ? OFF : ON;

        await SetNumberDataAsync([
            next,
            numbers.ValueAt(ROOT_X_INDEX),
            numbers.ValueAt(ROOT_Y_INDEX),
            numbers.ValueAt(WIDTH_INDEX),
            numbers.ValueAt(LENGTH_INDEX),
            numbers.ValueAt(INVISIBILITY_INDEX),
            numbers.ValueAt(WALL_ITEMS_INDEX),
            numbers.ValueAt(INVERT_INDEX),
        ]);
        await AnnounceAsync();
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
            || !_roomGrain.MapModule.InBounds(area.RootX, area.RootY)
        )
            return Reject(ctx, interaction, "area out of range");

        await SetNumberDataAsync([
            numbers.ValueAt(STATE_INDEX),
            area.RootX,
            area.RootY,
            area.Width,
            area.Length,
            area.Invisibility ? 1 : 0,
            area.WallItems ? 1 : 0,
            area.Invert ? 1 : 0,
        ]);
        await AnnounceAsync();

        return true;
    }

    private Task AnnounceAsync()
    {
        if (StuffData is not INumberStuffData numbers)
            return Task.CompletedTask;

        return _ctx.SendComposerToRoomAsync(
            new AreaHideMessageComposer
            {
                AreaHideData = new AreaHideDataSnapshot
                {
                    FurniId = _ctx.ObjectId,
                    On = numbers.ValueAt(STATE_INDEX) == ON,
                    RootX = numbers.ValueAt(ROOT_X_INDEX),
                    RootY = numbers.ValueAt(ROOT_Y_INDEX),
                    Width = numbers.ValueAt(WIDTH_INDEX),
                    Length = numbers.ValueAt(LENGTH_INDEX),
                    Invert = numbers.ValueAt(INVERT_INDEX) == 1,
                },
            }
        );
    }
}
