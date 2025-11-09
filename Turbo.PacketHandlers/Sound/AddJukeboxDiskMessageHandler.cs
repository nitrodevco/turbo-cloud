using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

public class AddJukeboxDiskMessageHandler : IMessageHandler<AddJukeboxDiskMessage>
{
    public async ValueTask HandleAsync(
        AddJukeboxDiskMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
