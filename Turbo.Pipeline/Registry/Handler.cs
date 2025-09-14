using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Delegates;

namespace Turbo.Pipeline.Registry;

internal record Handler<TContext>(
    string OwnerId,
    Type MessageType,
    Type ImplementationType,
    bool UseAmbientScope,
    IServiceScopeFactory ScopeFactory,
    Func<IServiceProvider, object> Create,
    HandlerInvoker<TContext> Invoke
);
