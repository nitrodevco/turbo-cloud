using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

public class GetJukeboxPlayListMessageHandler : IMessageHandler<GetJukeboxPlayListMessage>
{
    public async ValueTask HandleAsync(
        GetJukeboxPlayListMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
