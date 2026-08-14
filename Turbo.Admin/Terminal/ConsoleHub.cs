using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Turbo.Admin.Emulator;

namespace Turbo.Admin.Terminal;

[Authorize]
public sealed class ConsoleHub(
    ConsoleBroadcastService broadcastService,
    IEmulatorProcessSupervisor supervisor
) : Hub
{
    private readonly ConsoleBroadcastService _broadcastService = broadcastService;
    private readonly IEmulatorProcessSupervisor _supervisor = supervisor;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync().ConfigureAwait(false);

        await Clients
            .Caller.SendAsync("history", _broadcastService.GetHistory())
            .ConfigureAwait(false);
    }

    public Task SendCommandAsync(string input) =>
        _supervisor.SendInputAsync(input, Context.ConnectionAborted);
}
