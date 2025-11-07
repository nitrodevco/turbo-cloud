using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Turbo.Contracts.Plugins;

public interface IHostPluginModule
{
    public string Key { get; }
    void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder);
}
