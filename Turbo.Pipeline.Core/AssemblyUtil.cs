using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Turbo.Contracts.Attributes;

namespace Turbo.Pipeline.Core;

public static class AssemblyUtil
{
    // Optional: keep your own prefixes tight so you don't scan the world.
    private static readonly string[] NamespaceAllowlistPrefixes =
    [
        "Turbo", // Turbo.*, Turbo
    ];

    private static readonly string[] InfraNamespaceBlocklistPrefixes = ["System.", "Microsoft."];

    public static List<Assembly> GetLoadedAssemblies()
    {
        return [.. AppDomain.CurrentDomain.GetAssemblies().Where(a => HasTurboScan(a))];
    }

    public static bool HasTurboScan(Assembly a, string scope = "") =>
        a.GetCustomAttributes<TurboScan>().Any(att => att.Scope == scope);

    public static IEnumerable<Type> SafeGetLoadableTypes(Assembly assembly)
    {
        if (assembly is null)
            throw new ArgumentNullException(nameof(assembly));
        if (assembly.IsDynamic)
            yield break; // nothing concrete to scan

        Type[] raw;
        try
        {
            raw = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            raw = [.. ex.Types.Where(t => t is not null)!];
        }

        foreach (var t in raw)
        {
            if (t is null)
                continue;
            if (!IsCandidateType(t))
                continue;

            yield return t;
        }
    }

    private static bool IsCandidateType(Type t)
    {
        if (t.IsInterface)
            return false;
        if (t.IsAbstract)
            return false;
        if (t.IsGenericTypeDefinition)
            return false;
        if (t.IsPointer || t.IsByRef)
            return false;

        if (IsAnonymousType(t))
            return false;
        if (IsCompilerOrToolGenerated(t))
            return false;
        if (IsProxyOrInfraType(t))
            return false;

        // 3) (Optional) Limit to your own namespaces; keep internals allowed
        //     Comment this block if you purposely scan 3rd-party libraries.
        if (t.Namespace is string ns && !NamespaceAllowlistPrefixes.Any(ns.StartsWith))
            return false;

        return true;
    }

    private static bool IsAnonymousType(Type t)
    {
        if (!Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute), inherit: false))
            return false;

        var n = t.Name;
        if (!n.Contains("AnonymousType", StringComparison.Ordinal))
            return false;

        var isCSharpPattern = n.StartsWith("<>", StringComparison.Ordinal);
        var isVBPattern = n.StartsWith("VB$", StringComparison.Ordinal);

        var isNonPublic = (t.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;

        return (isCSharpPattern || isVBPattern) && t.IsSealed && t.IsGenericType && isNonPublic;
    }

    private static bool IsCompilerOrToolGenerated(Type t)
    {
        return Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute), inherit: false)
            || Attribute.IsDefined(t, typeof(GeneratedCodeAttribute), inherit: false)
            || Attribute.IsDefined(t, typeof(DebuggerNonUserCodeAttribute), inherit: false);
    }

    private static bool IsProxyOrInfraType(Type t)
    {
        if (t.Namespace is string ns && InfraNamespaceBlocklistPrefixes.Any(ns.StartsWith))
            return true;

        var name = t.FullName ?? t.Name;
        if (name.Contains("Proxy", StringComparison.Ordinal))
            return true;

        if (name.StartsWith("<>", StringComparison.Ordinal))
            return true;

        return false;
    }
}
