using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins;

public interface IPluginDbModule
{
    Task MigrateAsync(IServiceProvider sp, CancellationToken ct);
    Task UninstallAsync(IServiceProvider sp, CancellationToken ct);
}
