using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.Authentication;

public sealed class AuthenticationModule : IHostPluginModule
{
    public void ConfigureServices(IServiceCollection services) { }
}
