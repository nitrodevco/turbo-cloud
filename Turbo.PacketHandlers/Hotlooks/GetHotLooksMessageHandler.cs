using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Incoming.Hotlooks;
using Turbo.Primitives.Messages.Outgoing.Hotlooks;

namespace Turbo.PacketHandlers.Hotlooks;

/// <summary>
/// The avatar editor's Hot Looks tab (Flash <c>HotLooksModel</c>): the hotel's looks
/// (<c>Turbo:Players:HotLooks</c>), at most as many as asked for.
/// </summary>
public class GetHotLooksMessageHandler(IOptions<PlayerConfig> playerConfig)
    : IMessageHandler<GetHotLooksMessage>
{
    private readonly PlayerConfig _playerConfig = playerConfig.Value;

    public async ValueTask HandleAsync(
        GetHotLooksMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new HotLooksMessageComposer
                {
                    Looks =
                    [
                        .. _playerConfig
                            .HotLooks.Where(x => !string.IsNullOrWhiteSpace(x.Figure))
                            .Take(message.Amount)
                            .Select(x => (x.Gender, x.Figure)),
                    ],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
