using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// What a test host (Turbo.Testing's <c>TestHotel</c>, docs/plugin-testing.md) changes of how a
/// plugin loads, read from the host's services; a server registers none. The plugin's sources are
/// still compiled and held to the policy as on a server, so one that does not compile or is
/// refused fails its load the same way; it then runs as the test's own build of those sources, so
/// the test and the plugin share its types.
/// </summary>
internal sealed class PluginTestHooks
{
    /// <summary>The test's build of the plugin, run in place of the compiled one; null runs the compiled one.</summary>
    public Func<PluginManifest, Assembly?> BuiltCode { get; init; } = _ => null;

    /// <summary>
    /// The test's changes to the plugin's own services (the hotel's clock for its
    /// <see cref="TimeProvider"/>, a fake of its own), after the plugin's and the conventional ones.
    /// </summary>
    public Action<PluginManifest, IServiceCollection>? Services { get; init; }

    /// <summary>The host's services as the plugin's classes reach them; the host's own when null.</summary>
    public Func<IServiceProvider, IServiceProvider>? PluginHost { get; init; }
}
