using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Turbo.Pipeline.Abstractions;

public static class TypeUtil
{
    public static IEnumerable<Type> SafeGetTypes(Assembly a)
    {
        try
        {
            return a.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
