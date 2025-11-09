using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;

namespace Turbo.PacketHandlers.Moderator;

public class GetModeratorUserInfoMessageHandler : IMessageHandler<GetModeratorUserInfoMessage>
{
    public async ValueTask HandleAsync(
        GetModeratorUserInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
