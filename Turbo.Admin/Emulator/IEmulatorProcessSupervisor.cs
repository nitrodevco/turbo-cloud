using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Admin.Emulator;

public interface IEmulatorProcessSupervisor
{
    EmulatorStatus Status { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task RestartAsync(CancellationToken ct);
    Task SendInputAsync(string line, CancellationToken ct);
}
