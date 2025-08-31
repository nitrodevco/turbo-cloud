using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Events.Registry;
using Turbo.Events.Players;

namespace Turbo.Main.EventHandlers;

public sealed class WelcomeOnJoin(ILogger<PlayerJoinedEvent> logger)
    : IEventBehavior<PlayerJoinedEvent>
{
    private readonly ILogger<PlayerJoinedEvent> _logger = logger;

    public async Task InvokeAsync(
        PlayerJoinedEvent e,
        EventContext ctx,
        Func<Task> next,
        CancellationToken ct
    )
    {
        _logger.LogDebug("Begin PlayerJoined {Id}", e.PlayerId);
        try
        {
            await next();
        }
        finally
        {
            _logger.LogDebug("End PlayerJoined {Id}", e.PlayerId);
        }
    }
}
