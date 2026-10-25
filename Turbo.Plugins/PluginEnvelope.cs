using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

internal sealed class PluginEnvelope : AssemblyDescriptor
{
    public required PluginManifest Manifest { get; init; }
    public required string Folder { get; init; }

    /// <summary>Its code files when it was loaded: <see cref="PluginHelpers.CodeFingerprint"/>.</summary>
    public required string Fingerprint { get; init; }
    public required ITurboPlugin Instance { get; init; }
    public required IServiceProvider ServiceProvider { get; init; }
    public required List<object> Disposables { get; init; }
    public required PluginExtensions Extensions { get; init; }

    /// <summary>Its hotel-ready hook, once it has been called; an unload waits for it.</summary>
    public Task? Ready { get; set; }
}
