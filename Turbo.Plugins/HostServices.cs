using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// The host's services as one plugin reaches them. The extension packet registry it is given
/// notes what the plugin registers (<see cref="PluginExtensions"/>), and the client module registry
/// serves from its own folder (<see cref="PluginClientModules"/>). A replaceable service is the
/// one under the plugin's own replacement once it has one (<see cref="ServiceSlot{T}.For"/>), never
/// the plugin's own. As a service provider it is what the plugin's own registered classes are built
/// over after its container, so they get the same.
/// </summary>
internal sealed class HostServices(
    IServiceProvider host,
    string owner,
    PluginExtensions extensions,
    PluginClientModules? clientModules = null
) : IHostServices, IServiceProvider
{
    private readonly IServiceProvider _host = host;

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IClientModuleRegistry) && clientModules is not null)
            return clientModules;

        if (
            _host.GetService<IReplaceableServices>()?.IsReplaceable(serviceType) == true
            && _host.GetService(typeof(IServiceSlot<>).MakeGenericType(serviceType))
                is IServiceSlotEntries slot
        )
            return slot.For(owner);

        var service = _host.GetService(serviceType);

        return
            service is IExtensionPacketRegistry registry
            && serviceType == typeof(IExtensionPacketRegistry)
            ? extensions.Wrap(registry)
            : service;
    }

    public T GetRequiredService<T>()
        where T : notnull =>
        GetService(typeof(T)) is { } service ? (T)service : _host.GetRequiredService<T>();
}
