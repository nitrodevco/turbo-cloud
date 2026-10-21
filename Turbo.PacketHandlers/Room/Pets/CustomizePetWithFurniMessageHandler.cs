using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Pets;

/// <summary>
/// A pet product (saddle, dye, revival potion) is used on a pet: one standing in the room, named
/// by its object id, or one in the inventory, named by its inventory ref. Flash's use button
/// sends the ref (<c>FurniModel.showUseProductSelection</c>), which for a floor item is its id
/// negated (<c>FurnitureItemSerializer.WriteHead</c>), so a negative id is an inventory item.
/// </summary>
public class CustomizePetWithFurniMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CustomizePetWithFurniMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CustomizePetWithFurniMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.PetId <= 0)
            return;

        if (message.ObjectId < 0)
        {
            if (ctx.RoomId <= 0)
                return;

            await _grainFactory
                .GetRoomGrain(ctx.RoomId)
                .UsePetProductFromInventoryAsync(
                    ctx.AsActionContext(),
                    -message.ObjectId.Value,
                    message.PetId,
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new UseWithPetInteraction { PetId = message.PetId },
                ct
            )
            .ConfigureAwait(false);
    }
}
