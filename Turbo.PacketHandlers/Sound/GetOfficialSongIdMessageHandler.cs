using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

public class GetOfficialSongIdMessageHandler : IMessageHandler<GetOfficialSongIdMessage>
{
    public async ValueTask HandleAsync(
        GetOfficialSongIdMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
