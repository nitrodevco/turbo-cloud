using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Pets;

namespace Turbo.PacketHandlers.Room.Pets;

/// <summary>A pet product standing in the room (saddle, revival potion) is used on a pet.</summary>
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

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new UseWithPetInteraction { PetId = message.PetId },
                ct
            )
            .ConfigureAwait(false);
    }
}
