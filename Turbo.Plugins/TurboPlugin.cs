using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Core.Plugins;

namespace Turbo.Plugins;

public abstract class TurboPlugin : ITurboPlugin
{
    public abstract string PluginName { get; }
    public abstract string PluginAuthor { get; }

    public virtual void ConfigureHost(IHostBuilder hostBuilder)
    {
        // Default implementation does nothing
    }

    public virtual void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Default implementation does nothing
    }
}
