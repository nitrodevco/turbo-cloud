using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Turbo.Contracts.Plugins;

public interface ITurboPlugin
{
    string PluginName { get; }
    string PluginAuthor { get; }

    void ConfigureHost(IHostBuilder host);

    void ConfigureServices(HostBuilderContext context, IServiceCollection services);
}
