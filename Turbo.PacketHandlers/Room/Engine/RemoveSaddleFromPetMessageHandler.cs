using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;

namespace Turbo.PacketHandlers.Room.Engine;

public class RemoveSaddleFromPetMessageHandler : IMessageHandler<RemoveSaddleFromPetMessage>
{
    public async ValueTask HandleAsync(
        RemoveSaddleFromPetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
