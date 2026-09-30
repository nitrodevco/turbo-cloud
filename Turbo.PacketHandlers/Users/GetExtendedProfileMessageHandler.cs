using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// A player's extended profile (<c>GetExtendedProfileMessageComposer</c>) as the asker may see it.
/// The work is <see cref="IPlayerService"/>'s.
/// </summary>
public class GetExtendedProfileMessageHandler(IPlayerService playerService)
    : IMessageHandler<GetExtendedProfileMessage>
{
    private readonly IPlayerService _playerService = playerService;

    public async ValueTask HandleAsync(
        GetExtendedProfileMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var reply = await _playerService
            .GetExtendedProfileAsync(ctx.PlayerId, message.UserId, ct)
            .ConfigureAwait(false);

        if (reply is not null)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
