using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetAllVariablesDiffsMessageHandler
    : IMessageHandler<WiredGetAllVariablesDiffsMessage>
{
    public async ValueTask HandleAsync(
        WiredGetAllVariablesDiffsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
