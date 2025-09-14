using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Abstractions.Delegates;

namespace Turbo.Pipeline.Core.Registry;

public record Handler<TContext>(
    string OwnerId,
    Type MessageType,
    Type ImplementationType,
    bool UseAmbientScope,
    IServiceScopeFactory ScopeFactory,
    Func<IServiceProvider, object> Create,
    HandlerInvoker<TContext> Invoke
);
