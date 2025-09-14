using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Abstractions.Delegates;

namespace Turbo.Pipeline.Core.Registry;

public record Behavior<TContext>(
    string OwnerId,
    Type MessageType,
    Type ImplementationType,
    int Order,
    bool UseAmbientScope,
    IServiceScopeFactory ScopeFactory,
    Func<IServiceProvider, object> Create,
    BehaviorInvoker<TContext> Invoke
);
