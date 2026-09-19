using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

public class GiveSupplementToPetMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GiveSupplementToPetMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GiveSupplementToPetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.PetId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GivePetSupplementAsync(ctx.AsActionContext(), message.PetId, message.Supplement, ct)
            .ConfigureAwait(false);
    }
}
