using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins;

public interface IPluginDbModule
{
    Task MigrateAsync(IServiceProvider pluginSp, CancellationToken ct);
    Task UninstallAsync(IServiceProvider pluginSp, CancellationToken ct); // optional
}
