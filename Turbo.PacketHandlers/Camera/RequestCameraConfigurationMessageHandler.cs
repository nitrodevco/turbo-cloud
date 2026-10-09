using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;

namespace Turbo.PacketHandlers.Camera;

/// <summary><c>CameraWidgetHandler.sendInitCameraMessage</c>: the prices the dialog shows.</summary>
public class RequestCameraConfigurationMessageHandler(IOptions<CameraConfig> config)
    : IMessageHandler<RequestCameraConfigurationMessage>
{
    private readonly CameraConfig _config = config.Value;

    public async ValueTask HandleAsync(
        RequestCameraConfigurationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new InitCameraMessageComposer
                {
                    CreditPrice = _config.CreditPrice,
                    DucketPrice = _config.DucketPrice,
                    PublishDucketPrice = _config.PublishDucketPrice,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
