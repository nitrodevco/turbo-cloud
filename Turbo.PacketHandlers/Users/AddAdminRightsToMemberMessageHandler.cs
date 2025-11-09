using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class AddAdminRightsToMemberMessageHandler : IMessageHandler<AddAdminRightsToMemberMessage>
{
    public async ValueTask HandleAsync(
        AddAdminRightsToMemberMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
