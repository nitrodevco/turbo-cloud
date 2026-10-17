using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Networking.Extensions;

namespace Turbo.Plugins;

/// <summary>
/// The host's services as one plugin reaches them. The extension packet registry it is given
/// notes what the plugin registers (<see cref="PluginExtensions"/>).
/// </summary>
internal sealed class HostServices(IServiceProvider host, PluginExtensions extensions)
    : IHostServices
{
    private readonly IServiceProvider _host = host;

    public T GetRequiredService<T>()
        where T : notnull
    {
        var service = _host.GetRequiredService<T>();

        return
            service is IExtensionPacketRegistry registry
            && typeof(T) == typeof(IExtensionPacketRegistry)
            ? (T)extensions.Wrap(registry)
            : service;
    }
}
