using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;

namespace Turbo.PacketHandlers.Room.Engine;

public class RemovePetFromFlatMessageHandler : IMessageHandler<RemovePetFromFlatMessage>
{
    public async ValueTask HandleAsync(
        RemovePetFromFlatMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
