using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// A player's relationship statuses, for their profile and their info stand. The work is <see
/// cref="IPlayerService"/>'s.
/// </summary>
public class GetRelationshipStatusInfoMessageHandler(IPlayerService playerService)
    : IMessageHandler<GetRelationshipStatusInfoMessage>
{
    private readonly IPlayerService _playerService = playerService;

    public async ValueTask HandleAsync(
        GetRelationshipStatusInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _playerService
            .GetRelationshipStatusInfoAsync(ctx.PlayerId, message.PlayerId, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
