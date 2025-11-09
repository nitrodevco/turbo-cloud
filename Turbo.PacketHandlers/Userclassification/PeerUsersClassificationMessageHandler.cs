using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userclassification;

namespace Turbo.PacketHandlers.Userclassification;

public class PeerUsersClassificationMessageHandler : IMessageHandler<PeerUsersClassificationMessage>
{
    public async ValueTask HandleAsync(
        PeerUsersClassificationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
