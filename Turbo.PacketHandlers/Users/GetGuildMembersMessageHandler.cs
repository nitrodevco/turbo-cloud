using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class GetGuildMembersMessageHandler : IMessageHandler<GetGuildMembersMessage>
{
    public async ValueTask HandleAsync(
        GetGuildMembersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
