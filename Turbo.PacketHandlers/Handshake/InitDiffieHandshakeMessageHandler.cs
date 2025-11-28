using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.PacketHandlers.Handshake;

public class InitDiffieHandshakeMessageHandler(IDiffieService diffieService)
    : IMessageHandler<InitDiffieHandshakeMessage>
{
    private readonly IDiffieService _diffieService = diffieService;

    public async ValueTask HandleAsync(
        InitDiffieHandshakeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var prime = _diffieService.GetSignedPrime();
        var generator = _diffieService.GetSignedGenerator();

        await ctx.SendComposerAsync(
                new InitDiffieHandshakeMessageComposer { Prime = prime, Generator = generator },
                ct
            )
            .ConfigureAwait(false);
    }
}
