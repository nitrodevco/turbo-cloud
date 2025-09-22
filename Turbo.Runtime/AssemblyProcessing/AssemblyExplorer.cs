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

    public static Type? FindType(Assembly asm, Type type)
    {
        using var _ = EnterContextual(asm);

        Type? candidate = null;

        foreach (var ti in asm.DefinedTypes)
        {
            if (ti.IsAbstract || ti.IsInterface || ti.IsGenericTypeDefinition)
                continue;

            Type? asType = null;

            try
            {
                asType = ti.AsType();
            }
            catch
            {
                continue;
            }

            try
            {
                if (type.IsAssignableFrom(asType))
                {
                    if (candidate is null)
                    {
                        candidate = asType;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Multiple ITurboPlugin implementers in assembly {asm.GetName().Name}"
                        );
                    }
                }
            }
            catch
            {
                // ignore reflection oddities and keep scanning
            }
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

    private static AssemblyLoadContext.ContextualReflectionScope? EnterContextual(Assembly asm)
    {
        var alc = AssemblyLoadContext.GetLoadContext(asm);

        return alc?.EnterContextualReflection();
    }
}
