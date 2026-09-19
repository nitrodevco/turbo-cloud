using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Pets;

public class CancelPetBreedingMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CancelPetBreedingMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CancelPetBreedingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.NestId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .CancelNestBreedingAsync(ctx.AsActionContext(), message.NestId, ct)
            .ConfigureAwait(false);
    }
}
