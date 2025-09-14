using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Turbo.Plugins;

internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string mainAssemblyPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

        Resolving += OnResolving;
    }

    private Assembly? OnResolving(AssemblyLoadContext alc, AssemblyName name)
    {
        // Share host assemblies
        if (name.Name is string n && n.StartsWith("Turbo.", StringComparison.Ordinal))
        {
            var hostAsm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a =>
                a.GetName().Name == n
            );
            if (hostAsm is not null)
                return hostAsm;
        }

        // Otherwise, resolve from the plugin folder
        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override Assembly? Load(AssemblyName name)
    {
        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string name)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(name);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
