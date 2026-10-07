using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Vault;

/// <summary>The wired menu's lock and unlock buttons, for the player's chests or every chest.</summary>
public class LockAllChestsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<LockAllChestsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        LockAllChestsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .LockWiredChestsAsync(ctx.AsActionContext(), message.Lock, message.All, ct)
            .ConfigureAwait(false);
    }
}
