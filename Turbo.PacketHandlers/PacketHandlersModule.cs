using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.PacketHandlers;

public sealed class PacketHandlersModule : IHostPluginModule
{
    public void ConfigureServices(IServiceCollection services) { }
}
