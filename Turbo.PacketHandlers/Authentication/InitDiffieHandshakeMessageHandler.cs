using System.Threading;
using System.Threading.Tasks;
using Turbo.Crypto;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.PacketHandlers.Authentication;

public class InitDiffieHandshakeMessageHandler(DiffieService diffieService)
    : IMessageHandler<InitDiffieHandshakeMessage>
{
    private readonly DiffieService _diffieService = diffieService;

    public async ValueTask HandleAsync(
        InitDiffieHandshakeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var prime = _diffieService.GetSignedPrime();
        var generator = _diffieService.GetSignedGenerator();

        await ctx
            .Session.SendComposerAsync(
                new InitDiffieHandshakeMessageComposer { Prime = prime, Generator = generator },
                ct
            )
            .ConfigureAwait(false);
    }
}
