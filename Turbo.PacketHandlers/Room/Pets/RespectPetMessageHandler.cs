using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Pets;

namespace Turbo.PacketHandlers.Room.Pets;

public class RespectPetMessageHandler : IMessageHandler<RespectPetMessage>
{
    public async ValueTask HandleAsync(
        RespectPetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
