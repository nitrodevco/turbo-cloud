using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Directory;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Game.Directory;

/// <summary>Joins the lobby of the game named by its key. The directory sends the lobby (or the refusal) itself.</summary>
public class Game2StartSnowWarMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<Game2StartSnowWarMessage>
{
    public async ValueTask HandleAsync(
        Game2StartSnowWarMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await grainFactory
            .GetGameDirectoryGrain()
            .JoinAsync(ctx.PlayerId, message.Key, ct)
            .ConfigureAwait(false);
    }
}
