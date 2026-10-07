using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Locks or unlocks a chest and sets how full it may get.</summary>
public class SetChestOptionsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetChestOptionsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetChestOptionsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new SetChestOptionsInteraction
                {
                    Locked = message.Locked,
                    AutoLock = message.AutoLock,
                    Capacity = message.Capacity,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
