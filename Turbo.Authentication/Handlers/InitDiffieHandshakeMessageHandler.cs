using System.Threading;
using System.Threading.Tasks;
using Turbo.Crypto;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.Authentication.Handlers;

public class InitDiffieHandshakeMessageHandler(DiffieService diffieService)
    : IMessageHandler<Primitives.Messages.Incoming.Handshake.InitDiffieHandshakeMessage>
{
    private readonly DiffieService _diffieService = diffieService;

    public async ValueTask HandleAsync(
        Primitives.Messages.Incoming.Handshake.InitDiffieHandshakeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var prime = _diffieService.GetSignedPrime();
        var generator = _diffieService.GetSignedGenerator();

        await ctx
            .Session.SendComposerAsync(
                new Primitives.Messages.Outgoing.Handshake.InitDiffieHandshakeMessage
                {
                    Prime = prime,
                    Generator = generator,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
