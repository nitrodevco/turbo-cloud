using System;

namespace Turbo.Contracts.Plugins;

public interface IPluginContext
{
    IServiceProvider Services { get; }
    PluginRuntimeConfig RuntimeConfig { get; }
    PluginManifest Manifest { get; }
}
