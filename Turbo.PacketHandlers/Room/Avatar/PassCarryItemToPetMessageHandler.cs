using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;

namespace Turbo.PacketHandlers.Room.Avatar;

/// <summary>
/// Feeding a pet the carried item. Pets do not exist yet; see the room plan, phase 5.
/// </summary>
public class PassCarryItemToPetMessageHandler(ILogger<PassCarryItemToPetMessageHandler> logger)
    : IMessageHandler<PassCarryItemToPetMessage>
{
    private readonly ILogger<PassCarryItemToPetMessageHandler> _logger = logger;

    public async ValueTask HandleAsync(
        PassCarryItemToPetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.PetId <= 0)
            return;

        // Pets are not in the room system yet (plan phase 5); the packet is parsed so it is
        // visible in logs rather than silently dropped.
        _logger.LogDebug(
            "Player {PlayerId} tried to feed pet {PetId} in room {RoomId}; pets are not implemented",
            ctx.PlayerId,
            message.PetId,
            ctx.RoomId
        );

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
