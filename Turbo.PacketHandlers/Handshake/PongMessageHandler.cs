using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.PacketHandlers.Handshake;

/// <summary>
/// The client's answer to the heartbeat's Ping. Nothing is left to do here: every packet's
/// arrival is recorded before it is handled (<c>PackageHandler</c> marks the session), and that
/// is what keeps the connection from being closed as silent.
/// </summary>
public class PongMessageHandler : IMessageHandler<PongMessage>
{
    public async ValueTask HandleAsync(
        PongMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
