using System;
using Turbo.Pipeline.Delegates;

namespace Turbo.Pipeline.Registry;

/// <param name="ResolutionProvider">
/// What the behavior is activated from: the host's provider with the behavior's own behind it.
/// Built once at registration rather than for every message.
/// </param>
internal sealed record BehaviorReg<TContext>(
    Type BehaviorType,
    IServiceProvider ServiceProvider,
    IServiceProvider ResolutionProvider,
    int Order,
    Func<IServiceProvider, object> Activator,
    BehaviorInvoker<TContext> Invoker
);
