using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Admin.Hosting;

public interface IAdminWebHost
{
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}
