using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
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
/// <see cref="PresentStorage.SECTION"/> names the wrapped furniture row, which sits in the
/// present owner's inventory. Opening destroys the wrapping, places a floor item where the present
/// stood (or leaves a wall item in the inventory), and tells the opener what they got.
/// </summary>
[RoomObjectLogic("present")]
public class FurniturePresentLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    protected override StuffDataType _stuffDataType => StuffDataType.MapKey;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not OpenPresentInteraction)
            return false;

        if (_ctx.RoomObject.OwnerId != ctx.PlayerId)
            return Reject(ctx, interaction, "not the owner");

        var storage = ReadStorage();

        if (storage is null)
            return Reject(ctx, interaction, "no wrapped item recorded");

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(ctx.PlayerId);
        var wrapped = await inventory.GetItemSnapshotAsync(storage.ItemId, ct);

        if (wrapped is null)
            return Reject(ctx, interaction, "wrapped item is not in the owner's inventory");

        var (x, y, rotation) = (_ctx.RoomObject.X, _ctx.RoomObject.Y, _ctx.RoomObject.Rotation);

        // The wrapping goes first so the tile is free for what was inside.
        await _roomGrain.ActionModule.DeleteItemByIdAsync(ctx, _ctx.ObjectId, ct);

        var placedInRoom = false;

        if (wrapped.Definition.ProductType == ProductType.Floor)
        {
            try
            {
                placedInRoom = await _roomGrain.ActionModule.PlaceFloorItemAsync(
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

        await SendToPlayerAsync(
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

        return true;
    }

    private PresentStorage? ReadStorage() =>
        FurnitureExtraDataSections.Read<PresentStorage>(
            _ctx.RoomObject.ExtraData,
            PresentStorage.SECTION,
            _roomGrain._logger
        );
}
