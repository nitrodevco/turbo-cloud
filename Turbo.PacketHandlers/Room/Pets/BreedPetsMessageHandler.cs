using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Pets;

public class BreedPetsMessageHandler(IGrainFactory grainFactory) : IMessageHandler<BreedPetsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        BreedPetsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.PetId <= 0 || message.OtherPetId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .BreedPetsAsync(
                ctx.AsActionContext(),
                message.Action,
                message.PetId,
                message.OtherPetId,
                ct
            )
            .ConfigureAwait(false);
    }
}
