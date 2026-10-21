using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Tracking;

namespace Turbo.PacketHandlers.Handshake;

[AllowAnonymous]
public class ClientHelloMessageHandler(IClientTelemetry telemetry)
    : IMessageHandler<ClientHelloMessage>
{
    private readonly IClientTelemetry _telemetry = telemetry;

    public async ValueTask HandleAsync(
        ClientHelloMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.Production is null)
        {
            await ctx.CloseSessionAsync("client hello without a production").ConfigureAwait(false);

            return;
        }

        ctx.SetRevisionId(message.Production);
        _telemetry.RecordLoginStep(ctx.SessionKey, ctx.PlayerId, ClientLoginSteps.SOCKET_OK);

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
