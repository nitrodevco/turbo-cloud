using System;
using System.Linq;

namespace Turbo.Runtime;

/// <summary>
/// The host's services as a plugin's classes reach them (behind the plugin's own container). A
/// type of a plugin's own is not asked of the host: one declared in a collectible assembly, or
/// built from one (<c>IOptions&lt;ItsSettings&gt;</c>, an array of its type). The host's container
/// keeps every type it is asked for, found or not, for as long as the server runs, and with that
/// type the plugin's whole assembly context: a plugin whose class took a service nobody registered
/// stayed in memory after its failed load, each version again. The host registers none of them, so
/// the answer is the same: none.
/// </summary>
public sealed class PluginHostServiceProvider(IServiceProvider host) : IServiceProvider
{
    private readonly IServiceProvider _host = host;

    public object? GetService(Type serviceType) =>
        IsPluginType(serviceType) ? null : _host.GetService(serviceType);

    /// <summary>The type is a plugin's own or is built from one.</summary>
    public static bool IsPluginType(Type type)
    {
        if (type.HasElementType)
            return IsPluginType(type.GetElementType()!);

        if (type.Assembly.IsCollectible)
            return true;

        return type.IsConstructedGenericType && type.GenericTypeArguments.Any(IsPluginType);
    }
}
