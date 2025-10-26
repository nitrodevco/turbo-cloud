using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Contracts.Plugins;

public interface IHostPluginModule
{
    public string Key { get; }
    void ConfigureServices(IServiceCollection services);
}
