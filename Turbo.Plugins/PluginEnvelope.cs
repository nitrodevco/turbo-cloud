using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

public sealed class PluginEnvelope
{
    public required PluginManifest Manifest { get; init; }
    public required string Folder { get; init; }
    public required PluginLoadContext ALC { get; init; }
    public required Assembly Assembly { get; init; }
    public required ITurboPlugin Instance { get; init; }
    public required ServiceProvider Scope { get; init; }
    public required List<IDisposable> Disposables { get; init; }
}
