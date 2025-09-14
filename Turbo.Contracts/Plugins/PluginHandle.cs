using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Turbo.Contracts.Plugins;

public class PluginHandle
{
    public required string Id { get; init; }
    public required Version Version { get; init; }
    public required string ShadowDir { get; init; }
    public required AssemblyLoadContext Alc { get; init; }
    public required Assembly PluginAssembly { get; init; }
    public required IServiceProvider ServiceProvider { get; init; }
    public required ITurboPlugin Plugin { get; init; }
    public required List<IDisposable> Disposables { get; init; }
}
