using System;
using System.Collections.Generic;
using Turbo.Contracts.Plugins;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

internal sealed class PluginEnvelope : AssemblyDescriptor
{
    public required PluginManifest Manifest { get; init; }
    public required string Folder { get; init; }
    public required ITurboPlugin Instance { get; init; }
    public required IServiceProvider ServiceProvider { get; init; }
    public required List<object> Disposables { get; init; }
}
