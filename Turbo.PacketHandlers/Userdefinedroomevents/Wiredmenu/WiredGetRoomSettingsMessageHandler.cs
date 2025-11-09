using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetRoomSettingsMessageHandler : IMessageHandler<WiredGetRoomSettingsMessage>
{
    public async ValueTask HandleAsync(
        WiredGetRoomSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
