using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Admin.Terminal;

public interface IConsoleCommandExecutor
{
    Task HandleCommandAsync(string input, CancellationToken ct);
}
