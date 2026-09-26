using System;
using Turbo.Pipeline.Delegates;

namespace Turbo.Pipeline.Registry;

internal sealed record HandlerReg<TContext>(
    Type HandlerType,
    IServiceProvider ServiceProvider,
    Func<IServiceProvider, object> Activator,
    HandlerInvoker<TContext> Invoker
);
