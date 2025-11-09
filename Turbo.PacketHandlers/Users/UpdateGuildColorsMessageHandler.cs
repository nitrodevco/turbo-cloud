using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class UpdateGuildColorsMessageHandler : IMessageHandler<UpdateGuildColorsMessage>
{
    public async ValueTask HandleAsync(
        UpdateGuildColorsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
