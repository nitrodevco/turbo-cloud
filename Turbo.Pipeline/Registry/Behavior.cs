using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Delegates;

namespace Turbo.Pipeline.Registry;

internal record Behavior<TContext>(
    string OwnerId,
    Type MessageType,
    Type ImplementationType,
    int Order,
    bool UseAmbientScope,
    IServiceScopeFactory ScopeFactory,
    Func<IServiceProvider, object> Create,
    BehaviorInvoker<TContext> Invoke
);
