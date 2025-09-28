using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Contracts.Plugins;

public interface IHostPluginModule
{
    void ConfigureServices(IServiceCollection services);
}
