using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Web;

namespace Turbo.PacketHandlers.Turbo;

/// <summary>
/// A signed-in client asking for a token for plugins' web routes (<c>web.session</c>). Always
/// answered, with an empty token when the hotel serves none, so a client never waits.
/// </summary>
public class TurboWebSessionRequestMessageHandler(IPlayerWebTokens tokens)
    : IMessageHandler<TurboWebSessionRequestMessage>
{
    public async ValueTask HandleAsync(
        TurboWebSessionRequestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var token = ctx.PlayerId > 0 ? tokens.Issue(ctx.PlayerId) : null;

        await ctx.SendComposerAsync(
                new TurboWebSessionMessage
                {
                    BaseUrl = token?.BaseUrl ?? string.Empty,
                    Token = token?.Token ?? string.Empty,
                    ExpiresInSeconds = token?.ExpiresInSeconds ?? 0,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
