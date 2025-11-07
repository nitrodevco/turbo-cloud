using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;

namespace Turbo.PacketHandlers;

public sealed class PacketHandlersModule : IHostPluginModule
{
    public string Key => "turbo-packet-handlers";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder) { }
}
