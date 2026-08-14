using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Common;

namespace Turbo.Admin.Terminal;

internal sealed class ConsoleBroadcastBridge(
    ConsoleBroadcastService broadcastService,
    IHubContext<ConsoleHub> hubContext,
    ILogger<ConsoleBroadcastBridge> logger
) : IHostedService
{
    private readonly ConsoleBroadcastService _broadcastService = broadcastService;
    private readonly IHubContext<ConsoleHub> _hubContext = hubContext;
    private readonly ILogger<ConsoleBroadcastBridge> _logger = logger;

    public Task StartAsync(CancellationToken ct)
    {
        _broadcastService.LineWritten += OnLineWritten;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _broadcastService.LineWritten -= OnLineWritten;

        return Task.CompletedTask;
    }

    private void OnLineWritten(string line) =>
        _hubContext
            .Clients.All.SendAsync("line", line)
            .LogAndForget(_logger, "Failed to broadcast console line to hub clients");
}
