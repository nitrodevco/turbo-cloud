using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;

namespace Turbo.PacketHandlers.Camera;

public class PhotoCompetitionMessageHandler : IMessageHandler<PhotoCompetitionMessage>
{
    public async ValueTask HandleAsync(
        PhotoCompetitionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
