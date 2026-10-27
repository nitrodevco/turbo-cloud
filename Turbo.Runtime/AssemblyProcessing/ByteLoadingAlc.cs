using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Turbo.Runtime.AssemblyProcessing;

/// <summary>
/// The context one plugin's assembly runs in, collected when the plugin unloads. It holds the
/// plugin's own assembly, compiled by the server from its sources (<see cref="AssemblyMemoryLoader.Load"/>),
/// and nothing else: every assembly it names is the server's (the framework, the contracts, the
/// packages plugins build on), and it loads no native library.
/// It keeps no reference to the assembly it holds: one from the context to its own assembly
/// keeps the context from ever being collected.
/// </summary>
public sealed class ByteLoadingAlc() : AssemblyLoadContext(isCollectible: true)
{
    // The server's own: the default context resolves it.
    protected override Assembly? Load(AssemblyName assemblyName) => null;

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) =>
        throw new InvalidOperationException(
            $"A plugin loads no native library ({unmanagedDllName}): docs/plugin-loading.md."
        );
}
