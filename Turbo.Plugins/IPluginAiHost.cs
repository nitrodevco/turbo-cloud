using System;
using System.Reflection;
using Turbo.Contracts.Ai;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// The host's language models as each plugin is given them (<c>docs/ai-sdk.md</c>): its own
/// <see cref="IAi"/>, which knows whose calls they are (the manifest's <c>"ai"</c>, the owner's
/// allowance, the usage log), and, while it runs, its assembly and services, which a run that
/// waited on an approval is resumed with.
/// </summary>
public interface IPluginAiHost
{
    IAi ForPlugin(PluginManifest manifest);

    /// <summary>The plugin has started; disposing the result says it stopped.</summary>
    IDisposable Live(PluginManifest manifest, Assembly assembly, IServiceProvider services);
}
