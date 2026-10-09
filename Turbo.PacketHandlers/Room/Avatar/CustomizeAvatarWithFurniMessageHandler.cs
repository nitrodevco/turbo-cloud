using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;

namespace Turbo.PacketHandlers.Room.Avatar;

/// <summary>
/// "Use &amp; Bind Clothing" in the client's <c>PurchasableClothingConfirmationView</c>: the
/// clothing furni's sets become its owner's and the furni is used up.
/// </summary>
public class CustomizeAvatarWithFurniMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CustomizeAvatarWithFurniMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CustomizeAvatarWithFurniMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new BindClothingInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
