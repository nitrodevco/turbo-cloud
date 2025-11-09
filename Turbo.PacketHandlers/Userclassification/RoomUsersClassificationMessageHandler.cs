using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userclassification;

namespace Turbo.PacketHandlers.Userclassification;

public class RoomUsersClassificationMessageHandler : IMessageHandler<RoomUsersClassificationMessage>
{
    public async ValueTask HandleAsync(
        RoomUsersClassificationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
