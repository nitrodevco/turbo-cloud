using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Directory;
using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Game.Directory;

/// <summary>Sent by the toolbar's games icon and the friend bar's game notice: says whether the game centre is open and how many games the player has left.</summary>
public class Game2CheckGameDirectoryStatusMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<Game2CheckGameDirectoryStatusMessage>
{
    public async ValueTask HandleAsync(
        Game2CheckGameDirectoryStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var status = await grainFactory
            .GetGameDirectoryGrain()
            .GetDirectoryStatusAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new Game2GameDirectoryStatusMessageMessageComposer { Status = status },
                ct
            )
            .ConfigureAwait(false);
    }
}
