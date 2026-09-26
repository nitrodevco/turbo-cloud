using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>The owner named the pet in a package; the package hatches it.</summary>
public class OpenPetPackageMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<OpenPetPackageMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        OpenPetPackageMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new OpenPetPackageInteraction { Name = message.Name ?? string.Empty },
                ct
            )
            .ConfigureAwait(false);
    }
}
