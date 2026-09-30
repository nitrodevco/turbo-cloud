using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// A player's extended profile found by name (a <c>profile/</c> link). The work is <see
/// cref="IPlayerService"/>'s.
/// </summary>
public class GetExtendedProfileByNameMessageHandler(IPlayerService playerService)
    : IMessageHandler<GetExtendedProfileByNameMessage>
{
    private readonly IPlayerService _playerService = playerService;

    public async ValueTask HandleAsync(
        GetExtendedProfileByNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _playerService
            .GetExtendedProfileByNameAsync(ctx.PlayerId, message.UserName, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
