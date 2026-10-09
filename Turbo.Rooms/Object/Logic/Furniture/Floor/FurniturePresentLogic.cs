using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A wrapped gift. The map data carries what the client shows (sender, note); the extra data's
/// <see cref="PresentStorage.SECTION"/> the box and ribbon it is drawn with. The wrapped item is
/// the furniture row this present holds. Opening is Habbo's: the present turns to its opening
/// state (<see cref="PresentStates.OPENING"/>, the gift wrapped box's glow and confetti) for
/// <c>PresentOpenMs</c>; then that row is released into the opener's inventory, the wrapping is
/// destroyed, a floor item is placed where the present stood (or a wall item left in the
/// inventory), and the opener is told what they got.
/// <para>
/// Nothing leaves the present until the opening has played, so a present picked up or a room
/// unloaded meanwhile is still whole; it is drawn wrapped again whenever it is next loaded.
/// </para>
/// </summary>
[RoomObjectLogic(PresentData.LOGIC_NAME)]
public class FurniturePresentLogic : FurnitureFloorLogic
{
    protected override StuffDataType _stuffDataType => StuffDataType.MapKey;

    /// <summary>The opening is playing; another open is refused until it is over.</summary>
    private bool _opening;

    public FurniturePresentLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        // An opening cut short (a pickup, a room unload) left its state behind.
        if (StuffData.GetLegacyString() == PresentStates.OPENING.ToString())
            StuffData.SetState(PresentStates.CLOSED.ToString());
    }

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not OpenPresentInteraction)
            return false;

        if (!IsItemOwner(ctx))
            return Reject(ctx, interaction, "not the owner");

        if (_opening)
            return Reject(ctx, interaction, "already opening");

        _opening = true;

        await SetStateAsync(PresentStates.OPENING);

        TimerSystem.Schedule(
            _ctx.ObjectId,
            _roomGrain._roomConfig.PresentOpenMs,
            token => OpenedAsync(ctx, token)
        );

        return true;
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _opening = false;

        return base.OnPickupAsync(ctx, ct);
    }

    /// <summary>The opening has played: what the present holds comes out and the wrapping goes.</summary>
    private async Task OpenedAsync(ActionContext ctx, CancellationToken ct)
    {
        // Released before the wrapping goes: deleting a present that still holds its item would
        // drop the item back to whoever it was first given to, behind this inventory's back.
        var wrapped = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .UnwrapPresentAsync(_ctx.ObjectId, ct);

        if (wrapped is null)
        {
            _roomGrain._logger.LogWarning(
                "Present {ItemId} in room {RoomId} opened by player {PlayerId} holds nothing; it stays wrapped",
                _ctx.ObjectId,
                _ctx.RoomId,
                ctx.PlayerId
            );

            _opening = false;

            await SetStateAsync(PresentStates.CLOSED);

            return;
        }

        var (x, y, rotation) = (_ctx.RoomObject.X, _ctx.RoomObject.Y, _ctx.RoomObject.Rotation);

        // The wrapping goes first so the tile is free for what was inside.
        await ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);

        var placedInRoom = false;

        if (wrapped.Definition.ProductType == ProductType.Floor)
        {
            try
            {
                placedInRoom = await ActionModule.PlaceFloorItemAsync(
                    ctx,
                    wrapped,
                    x,
                    y,
                    rotation,
                    ct
                );
            }
            catch (TurboException ex)
            {
                // Nothing fits where the present stood; the item stays in the inventory.
                _roomGrain._logger.LogDebug(
                    ex,
                    "Unwrapped item {ItemId} could not be placed in room {RoomId}; it stays in the inventory",
                    wrapped.ItemId,
                    _ctx.RoomId
                );
            }
        }

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new PresentOpenedMessageComposer
            {
                ItemType = wrapped.Definition.ProductType.ToLegacyString(),
                ClassId = wrapped.Definition.SpriteId,
                ProductCode = wrapped.Definition.Name,
                PlacedItemId = wrapped.ItemId,
                PlacedItemType = wrapped.Definition.ProductType,
                PlacedInRoom = placedInRoom,
                PetFigureString = string.Empty,
            },
            ct
        );
    }

    /// <summary>The box and ribbon, which the client reads beside the map data to draw the present.</summary>
    public override int GetObjectExtra() =>
        FurnitureExtraDataSections
            .Read<PresentStorage>(
                _ctx.RoomObject.ExtraData,
                PresentStorage.SECTION,
                _roomGrain._logger
            )
            ?.GetObjectExtra()
        ?? 0;
}
