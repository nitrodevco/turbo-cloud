using System;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

public static class ActivatorHelpers
{
    public static Func<IServiceProvider, object> BuildActivator(Type concrete)
    {
        var factory = ActivatorUtilities.CreateFactory(concrete, Type.EmptyTypes);
        object Activator(IServiceProvider sp) => factory(sp, null);
        return Activator;
    }
}
