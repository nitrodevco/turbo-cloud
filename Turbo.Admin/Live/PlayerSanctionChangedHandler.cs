using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Registry;
using Turbo.Primitives.Moderation.Events;

namespace Turbo.Admin.Live;

/// <summary>A player was banned or their ban lifted: their page is out of date.</summary>
public sealed class PlayerSanctionChangedHandler(AdminLiveFeed feed)
    : IEventHandler<PlayerSanctionChangedEvent>
{
    public ValueTask HandleAsync(
        PlayerSanctionChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        feed.Note(player: env.PlayerId.Value);

        return ValueTask.CompletedTask;
    }
}
