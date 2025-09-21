using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Turbo.Plugins;

internal static partial class PluginHelpers
{
    public static string CreateShadowCopy(string sourceDir, string pluginId)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException(sourceDir);

        var root = Path.Combine(AppContext.BaseDirectory, "plugins-shadow", pluginId);
        Directory.CreateDirectory(root);

        var shadowDir = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shadowDir);

        CopyAll(new DirectoryInfo(sourceDir), new DirectoryInfo(shadowDir));
        return shadowDir;
    }

    public static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
                return;

            Directory.Delete(dir, true);
            return;
        }
        catch (Exception ex)
        {
            try
            {
                var parent = Path.GetDirectoryName(dir) ?? AppContext.BaseDirectory;
                var tomb = Path.Combine(
                    parent,
                    ".plugin-delete-failed-" + Guid.NewGuid().ToString("N")
                );
                Directory.Move(dir, tomb);
            }
            catch (Exception moveEx)
            {
                try
                {
                    Trace.TraceWarning(
                        "Failed to delete or move plugin directory '{0}': {1}; move error: {2}",
                        dir,
                        ex.Message,
                        moveEx.Message
                    );
                }
                catch { }
            }
        }
    }

    private static void CopyAll(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite: true);

        foreach (var dir in source.GetDirectories())
            CopyAll(dir, target.CreateSubdirectory(dir.Name));
    }

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
            raw = ex.Types.Where(t => t is not null).ToArray()!;
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
        var name = t.FullName ?? t.Name;
        if (name.Contains("Proxy", StringComparison.Ordinal))
            return true;

        if (name.StartsWith("<>", StringComparison.Ordinal))
            return true;

        return false;
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}
