using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Events;
using Turbo.Events.Abstractions.Registry;

namespace Turbo.Revision20240709.EventHandlers;

public sealed class WelcomeOnJoin(ILogger<PlayerJoinedEvent> logger)
    : IEventBehavior<PlayerJoinedEvent>,
        IEventHandler<PlayerJoinedEvent>
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

    public Task HandleAsync(PlayerJoinedEvent e, EventContext ctx, CancellationToken ct)
    {
        Console.WriteLine($"Welcome player {e.PlayerId}");
        return Task.CompletedTask;
    }
}
