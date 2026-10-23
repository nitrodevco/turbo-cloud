using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Directory;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Game.Directory;

/// <summary>The game centre's play button: joins the default game's lobby. The directory sends the lobby (or the refusal) itself.</summary>
public class Game2QuickJoinGameMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<Game2QuickJoinGameMessage>
{
    public async ValueTask HandleAsync(
        Game2QuickJoinGameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await grainFactory
            .GetGameDirectoryGrain()
            .JoinAsync(ctx.PlayerId, null, ct)
            .ConfigureAwait(false);
    }
}
