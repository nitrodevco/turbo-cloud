using System;
using Turbo.Pipeline.Delegates;

namespace Turbo.Pipeline.Registry;

/// <param name="ResolutionProvider">
/// What the handler is activated from: its own provider with the host's behind it. Built once
/// at registration rather than for every invocation.
/// </param>
internal sealed record HandlerReg<TContext>(
    Type HandlerType,
    IServiceProvider ServiceProvider,
    IServiceProvider ResolutionProvider,
    Func<IServiceProvider, object> Activator,
    HandlerInvoker<TContext> Invoker
);
