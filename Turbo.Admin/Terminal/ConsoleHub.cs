using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Turbo.Admin.Terminal;

[Authorize]
public sealed class ConsoleHub(
    ConsoleBroadcastService broadcastService,
    IConsoleCommandExecutor executor
) : Hub
{
    private readonly ConsoleBroadcastService _broadcastService = broadcastService;
    private readonly IConsoleCommandExecutor _executor = executor;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync().ConfigureAwait(false);

        await Clients
            .Caller.SendAsync("history", _broadcastService.GetHistory())
            .ConfigureAwait(false);
    }

    public Task SendCommandAsync(string input) =>
        _executor.HandleCommandAsync(input, Context.ConnectionAborted);
}
