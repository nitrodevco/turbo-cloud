using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Turbo.Runtime.AssemblyProcessing;

public static class AssemblyExplorer
{
    public static IEnumerable<Type> SafeConcreteTypes(Assembly asm)
    {
        using var _ = EnterContextual(asm);

        try
        {
            return [.. asm.GetTypes().Where(IsConcrete)];
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null && IsConcrete(t!))!;
        }
    }

    public static IEnumerable<(
        Type Concrete,
        Type ClosedInterface,
        Type[] Args
    )> FindClosedImplementations(Assembly asm, Type openGenericInterface)
    {
        foreach (var t in SafeConcreteTypes(asm))
        {
            foreach (var iface in t.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openGenericInterface)
                    yield return (t, iface, iface.GetGenericArguments());
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

    private static bool IsConcrete(Type t) =>
        !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition;

    private static AssemblyLoadContext.ContextualReflectionScope? EnterContextual(Assembly asm)
    {
        var alc = AssemblyLoadContext.GetLoadContext(asm);

        return alc?.EnterContextualReflection();
    }
}
