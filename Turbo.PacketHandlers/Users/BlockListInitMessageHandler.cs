using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Sends the players this one has blocked. The client keeps them (BlockedUsersManager) to draw
/// their avatars as blocked, hide their chat and tick the profile's blocked mark, so without it
/// a block made in an earlier session is forgotten until the next BlockUserUpdate.
/// </summary>
public class BlockListInitMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<BlockListInitMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        BlockListInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var blockedPlayerIds = await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .GetBlockedAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new BlockListMessageComposer
                {
                    BlockedUserIds = [.. blockedPlayerIds.Select(x => x.Value)],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
