using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class GetGuildEditorDataMessageHandler : IMessageHandler<GetGuildEditorDataMessage>
{
    public async ValueTask HandleAsync(
        GetGuildEditorDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
