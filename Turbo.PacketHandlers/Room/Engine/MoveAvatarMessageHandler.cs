using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;

namespace Turbo.PacketHandlers.Room.Engine;

public class MoveAvatarMessageHandler : IMessageHandler<MoveAvatarMessage>
{
    public async ValueTask HandleAsync(
        MoveAvatarMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) { }
}
