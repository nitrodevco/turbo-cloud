using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Networking.Extensions;

namespace Turbo.Plugins;

/// <summary>
/// The host's services as one plugin reaches them. The extension packet registry it is given
/// notes what the plugin registers (<see cref="PluginExtensions"/>), and the client module registry
/// serves from its own folder (<see cref="PluginClientModules"/>).
/// </summary>
internal sealed class HostServices(
    IServiceProvider host,
    PluginExtensions extensions,
    PluginClientModules? clientModules = null
) : IHostServices
{
    private readonly IServiceProvider _host = host;

    public T GetRequiredService<T>()
        where T : notnull
    {
        if (typeof(T) == typeof(IClientModuleRegistry) && clientModules is not null)
            return (T)(object)clientModules;

        var service = _host.GetRequiredService<T>();

        return
            service is IExtensionPacketRegistry registry
            && typeof(T) == typeof(IExtensionPacketRegistry)
            ? (T)extensions.Wrap(registry)
            : service;
    }
}
