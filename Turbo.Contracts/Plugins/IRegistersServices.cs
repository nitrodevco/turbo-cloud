using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Contracts.Plugins;

public interface IRegistersServices
{
    void ConfigureServices(IServiceCollection services);
}
