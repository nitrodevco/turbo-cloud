using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Crypto;
using Turbo.Crypto.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Handshake;

namespace Turbo.Authentication.Handlers;

public class CompleteDiffieHandshakeMessageHandler(
    DiffieService diffieService,
    IOptions<CryptoConfig> config
) : IMessageHandler<Primitives.Messages.Incoming.Handshake.CompleteDiffieHandshakeMessage>
{
    private readonly DiffieService _diffieService = diffieService;
    private readonly CryptoConfig _config = config.Value;

    public async ValueTask HandleAsync(
        Primitives.Messages.Incoming.Handshake.CompleteDiffieHandshakeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var sharedKey = _diffieService.GetSharedKey(message.SharedKey);

        await ctx
            .Session.SendComposerAsync(
                new Primitives.Messages.Outgoing.Handshake.CompleteDiffieHandshakeMessage
                {
                    PublicKey = _diffieService.GetPublicKey(),
                    ServerClientEncryption = _config.EnableServerToClientEncryption,
                },
                ct
            )
            .ConfigureAwait(false);

        ctx.Session.SetupEncryption(sharedKey, _config.EnableServerToClientEncryption);
    }
}
