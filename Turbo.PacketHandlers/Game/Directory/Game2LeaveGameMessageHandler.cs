using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Directory;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Game.Directory;

/// <summary>The lobby window's close: leaves the lobby, or the started game.</summary>
public class Game2LeaveGameMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<Game2LeaveGameMessage>
{
    public async ValueTask HandleAsync(
        Game2LeaveGameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await grainFactory
            .GetGameDirectoryGrain()
            .LeaveAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);
    }
}
