using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Directory;
using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Game.Directory;

/// <summary>Sent with the subscription status and after a game: the player's games left and played for one game.</summary>
public class Game2GetAccountGameStatusMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<Game2GetAccountGameStatusMessage>
{
    public async ValueTask HandleAsync(
        Game2GetAccountGameStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var status = await grainFactory
            .GetGameDirectoryGrain()
            .GetAccountStatusAsync(ctx.PlayerId, message.GameTypeId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new Game2AccountGameStatusMessageMessageComposer { Status = status },
                ct
            )
            .ConfigureAwait(false);
    }
}
