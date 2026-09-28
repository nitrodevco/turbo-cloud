using System;
using System.Collections.Generic;
using System.Reflection;

namespace Turbo.Tests.Support;

/// <summary>
/// What a compiled method body loads and calls, read from its IL. A byte-level scan rather than a
/// full decoder: an <c>ldstr</c> (0x72) or <c>call</c>/<c>callvirt</c> (0x28/0x6F) byte followed by
/// a token of the right table is taken as that instruction. A stray match can only add to what is
/// found, never hide what is there, which is the safe way round for the tests that use it.
/// </summary>
internal static class IlScanner
{
    public static void Scan(MethodBase method, ISet<string> strings, ISet<MethodBase>? calls = null)
    {
        byte[]? il;

        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException)
        {
            return;
        }

        if (il is null)
            return;

        var module = method.Module;

        for (var i = 0; i + 4 < il.Length; i++)
        {
            var token = BitConverter.ToInt32(il, i + 1);
            var table = (uint)token >> 24;

            if (il[i] == 0x72 && table == 0x70)
            {
                try
                {
                    strings.Add(module.ResolveString(token));
                }
                catch (ArgumentException) { }
            }
            else if (
                calls is not null
                && (il[i] == 0x28 || il[i] == 0x6F)
                && table is 0x06 or 0x0A or 0x2B
            )
            {
                try
                {
                    if (module.ResolveMethod(token) is { } called)
                        calls.Add(called);
                }
                catch (ArgumentException) { }
                catch (BadImageFormatException) { }
            }
        }
    }

    /// <summary>Every method, constructor and accessor a type declares, nested types and state machines included.</summary>
    public static IEnumerable<MethodBase> MethodsOf(Type type)
    {
        const BindingFlags ALL =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(ALL))
            yield return method;

        foreach (var constructor in type.GetConstructors(ALL))
            yield return constructor;

        foreach (var nested in type.GetNestedTypes(ALL))
        {
            foreach (var method in MethodsOf(nested))
                yield return method;
        }
    }
}
