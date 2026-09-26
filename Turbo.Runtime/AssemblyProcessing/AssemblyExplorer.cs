using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Turbo.Runtime.AssemblyProcessing;

public static class AssemblyExplorer
{
    private static readonly ConditionalWeakTable<Assembly, Lazy<Type[]>> CONCRETE_TYPE_CACHE = [];

    /// <summary>
    /// The single concrete public type in <paramref name="asm"/> assignable to
    /// <paramref name="type"/>, or null when there is none.
    /// </summary>
    /// <exception cref="InvalidOperationException">More than one type qualifies.</exception>
    public static Type? FindType(Assembly asm, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        using var _ = EnterContextual(asm);

        Type? candidate = null;

        foreach (var concrete in EnumerateConcreteTypes(asm))
        {
            if (!type.IsAssignableFrom(concrete))
                continue;

            if (candidate is not null)
                throw new InvalidOperationException(
                    $"Multiple {type.Name} implementers in assembly {asm.GetName().Name}"
                );

            candidate = concrete;
        }

        return candidate;
    }

    public static IEnumerable<(
        Type Concrete,
        Type ClosedInterface,
        Type[] Args
    )> FindClosedImplementations(Assembly asm, Type openGenericInterface)
    {
        ArgumentNullException.ThrowIfNull(openGenericInterface);

        if (!openGenericInterface.IsGenericTypeDefinition)
            throw new ArgumentException(
                "Must be an open generic, e.g. typeof(IFoo<>).",
                nameof(openGenericInterface)
            );

        using var _ = EnterContextual(asm);

        foreach (var concrete in EnumerateConcreteTypes(asm))
        {
            foreach (var iface in concrete.GetInterfaces())
            {
                if (
                    iface.IsGenericType
                    && ReferenceEquals(iface.GetGenericTypeDefinition(), openGenericInterface)
                )
                    yield return (concrete, iface, iface.GetGenericArguments());
            }
        }
    }

    public static IEnumerable<Type> FindAssignees(Assembly asm, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        using var _ = EnterContextual(asm);

        foreach (var concrete in EnumerateConcreteTypes(asm))
        {
            if (!concrete.IsClass || !targetType.IsAssignableFrom(concrete))
                continue;

            yield return concrete;
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

    // The types every scan considers: public, instantiable and closed. A type that cannot be
    // loaded (a plugin missing a dependency) throws out of here rather than being skipped, so the
    // plugin load fails with the reason instead of registering a partial set of features.
    private static IEnumerable<Type> EnumerateConcreteTypes(Assembly asm)
    {
        foreach (var ti in asm.DefinedTypes)
        {
            if (ti.IsAbstract || ti.IsInterface || ti.IsGenericTypeDefinition || !ti.IsPublic)
                continue;

            yield return ti.AsType();
        }
    }

    private static AssemblyLoadContext.ContextualReflectionScope? EnterContextual(Assembly asm)
    {
        var alc = AssemblyLoadContext.GetLoadContext(asm);

        return alc?.EnterContextualReflection();
    }
}
