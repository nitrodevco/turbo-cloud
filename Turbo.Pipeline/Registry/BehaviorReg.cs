using System;
using Turbo.Pipeline.Delegates;

namespace Turbo.Pipeline.Registry;

internal sealed record BehaviorReg<TContext>(
    Type BehaviorType,
    IServiceProvider ServiceProvider,
    int Order,
    Func<IServiceProvider, object> Activator,
    BehaviorInvoker<TContext> Invoker
);
