using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

namespace Turbo.Plugins;

public class TurboPluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly IReadOnlyList<Regex> _shareAllow;
    private readonly IReadOnlyList<Regex> _shareDeny;

    public TurboPluginLoadContext(
        string pluginAssemblyPath,
        IEnumerable<string>? shareAllowPatterns = null,
        IEnumerable<string>? shareDenyPatterns = null
    )
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);

        shareAllowPatterns ??= [];
        shareDenyPatterns ??= [];

        _shareAllow = shareAllowPatterns.Select(p => new Regex(p, RegexOptions.Compiled)).ToList();
        _shareDeny = shareDenyPatterns.Select(p => new Regex(p, RegexOptions.Compiled)).ToList();
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 1) If already loaded in Default, reuse it (prevents type splits)
        var defaultAsm = TryGetFromDefault(assemblyName);
        if (defaultAsm is not null)
            return defaultAsm;

        // 2) Policy: should this be shared from Default by *name* pattern?
        if (ShouldShare(assemblyName))
        {
            // Return null => let Default resolve it. If Default can’t, try to bind explicitly.
            var shared = TryLoadFromDefault(assemblyName);
            if (shared is not null)
                return shared;

            // As a last resort, fall through to private resolution
        }

        // 3) Private resolution for plugin-local deps
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static Assembly? TryGetFromDefault(AssemblyName name)
    {
        // Simple-name match + equal or higher version safeguard
        foreach (var asm in AssemblyLoadContext.Default.Assemblies)
        {
            var an = asm.GetName();
            if (!string.Equals(an.Name, name.Name, StringComparison.Ordinal))
                continue;

            // If plugin asks for <= version that Default already has, reuse it
            if (name.Version is null || an.Version >= name.Version)
                return asm;
        }
        return null;
    }

    private static Assembly? TryLoadFromDefault(AssemblyName name)
    {
        // Returning null in Load() already delegates to Default, but this helper
        // lets us *try* a bind without abandoning control entirely.
        try
        {
            return Default.LoadFromAssemblyName(name);
        }
        catch
        {
            return null;
        }
    }

    private bool ShouldShare(AssemblyName name)
    {
        var simple = name.Name ?? string.Empty;

        // Explicit deny wins
        if (_shareDeny.Any(rx => rx.IsMatch(simple)))
            return false;

        // Allow by pattern
        if (_shareAllow.Any(rx => rx.IsMatch(simple)))
            return true;

        return false;
    }
}
