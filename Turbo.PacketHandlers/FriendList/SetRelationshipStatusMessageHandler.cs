using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class SetRelationshipStatusMessageHandler : IMessageHandler<SetRelationshipStatusMessage>
{
    public async ValueTask HandleAsync(
        SetRelationshipStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
