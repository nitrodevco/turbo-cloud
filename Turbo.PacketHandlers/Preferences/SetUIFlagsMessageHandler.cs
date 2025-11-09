using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;

namespace Turbo.PacketHandlers.Preferences;

public class SetUIFlagsMessageHandler : IMessageHandler<SetUIFlagsMessage>
{
    public async ValueTask HandleAsync(
        SetUIFlagsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
