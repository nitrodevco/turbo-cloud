using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Pets;

public class ConfirmPetBreedingMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ConfirmPetBreedingMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ConfirmPetBreedingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || message.NestId <= 0
            || message.PetId <= 0
            || message.OtherPetId <= 0
        )
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ConfirmNestBreedingAsync(
                ctx.AsActionContext(),
                message.NestId,
                message.Name ?? string.Empty,
                message.PetId,
                message.OtherPetId,
                ct
            )
            .ConfigureAwait(false);
    }
}
