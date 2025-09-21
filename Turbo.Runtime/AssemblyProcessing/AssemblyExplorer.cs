using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;

namespace Turbo.Runtime.AssemblyProcessing;

public static class AssemblyExplorer
{
    private static readonly ConditionalWeakTable<Assembly, Lazy<Type[]>> CONCRETE_TYPE_CACHE = [];

    public static IEnumerable<Type> SafeConcreteTypes(Assembly asm) => GetOrCacheConcreteTypes(asm);

    public static IEnumerable<(
        Type Concrete,
        Type ClosedInterface,
        Type[] Args
    )> FindClosedImplementations(Assembly asm, Type openGenericInterface)
    {
        var types = GetOrCacheConcreteTypes(asm);

        foreach (var t in types)
        {
            foreach (var iface in t.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openGenericInterface)
                    yield return (t, iface, iface.GetGenericArguments());
            }
        }
    }

    public static IEnumerable<(
        Type Concrete,
        Type ClosedInterface,
        Type[] Args
    )> FindClosedImplementationsFast(Assembly asm, Type openGenericInterface)
    {
        ArgumentNullException.ThrowIfNull(openGenericInterface);

        if (!openGenericInterface.IsGenericTypeDefinition)
            throw new ArgumentException(
                "Must be an open generic, e.g. typeof(IFoo<>).",
                nameof(openGenericInterface)
            );

        using var _ = EnterContextual(asm);

        foreach (var ti in asm.DefinedTypes)
        {
            if (ti.IsAbstract || ti.IsInterface || ti.IsGenericTypeDefinition || !ti.IsPublic)
                continue;

            Type concrete;

            try
            {
                concrete = ti.AsType();
            }
            catch
            {
                continue;
            }

            IEnumerable<Type> ifaces;

            try
            {
                ifaces = ti.ImplementedInterfaces;
            }
            catch
            {
                continue;
            }

            foreach (var iface in ifaces)
            {
                bool match = false;
                Type[]? args = null;

                try
                {
                    if (
                        iface.IsGenericType
                        && ReferenceEquals(iface.GetGenericTypeDefinition(), openGenericInterface)
                    )
                    {
                        args = iface.GetGenericArguments();
                        match = true;
                    }
                }
                catch
                {
                    // ignore malformed interface
                }

                if (match && args is not null)
                    yield return (concrete, iface, args);
            }
        }
    }

    public static MethodInfo ResolveImplementation(
        Type concrete,
        Type closedIface,
        string ifaceMethodName
    )
    {
        var ifaceMethod =
            closedIface.GetMethod(ifaceMethodName)
            ?? throw new MissingMethodException(closedIface.FullName, ifaceMethodName);

        var map = concrete.GetInterfaceMap(closedIface);

        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            if (map.InterfaceMethods[i] == ifaceMethod)
                return map.TargetMethods[i];
        }

        var m =
            concrete.GetMethod(
                ifaceMethodName,
                ifaceMethod.GetParameters().Select(p => p.ParameterType).ToArray()
            ) ?? throw new MissingMethodException(concrete.FullName, ifaceMethodName);

        return m;
    }

    public static IDisposable CacheScope(Assembly asm)
    {
        _ = GetOrCacheConcreteTypes(asm);

        return new CacheScopeImpl(asm);
    }

    public static void ClearCache(Assembly asm) => CONCRETE_TYPE_CACHE.Remove(asm);

    private static Type[] GetOrCacheConcreteTypes(Assembly asm)
    {
        var lazy = CONCRETE_TYPE_CACHE.GetValue(
            asm,
            a => new Lazy<Type[]>(
                () => LoadConcreteTypes(a),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        return lazy.Value;
    }

    private static Type[] LoadConcreteTypes(Assembly asm)
    {
        using var _ = EnterContextual(asm);

        try
        {
            return [.. asm.GetExportedTypes().Where(IsConcrete)];
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.Where(t => t is not null && IsConcrete(t!))!];
        }
    }

    private static bool IsConcrete(Type t) =>
        !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition;

    private static AssemblyLoadContext.ContextualReflectionScope? EnterContextual(Assembly asm)
    {
        var alc = AssemblyLoadContext.GetLoadContext(asm);

        return alc?.EnterContextualReflection();
    }

    private sealed class CacheScopeImpl(Assembly asm) : IDisposable
    {
        private Assembly? _asm = asm;

        public void Dispose()
        {
            if (_asm is not null)
            {
                CONCRETE_TYPE_CACHE.Remove(_asm);

                _asm = null;
            }
        }
    }
}
