using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

public class SetYoutubeDisplayPlaylistMessageHandler
    : IMessageHandler<SetYoutubeDisplayPlaylistMessage>
{
    public async ValueTask HandleAsync(
        SetYoutubeDisplayPlaylistMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
