using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

public class GetSongInfoMessageHandler : IMessageHandler<GetSongInfoMessage>
{
    public async ValueTask HandleAsync(
        GetSongInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
