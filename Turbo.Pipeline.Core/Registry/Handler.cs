using System;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Core.Registry;

public record Handler<TContext>(
    string OwnerId,
    Type MessageType,
    bool UseAmbientScope,
    Func<IServiceScope>? BeginScope,
    Func<IServiceProvider, object> CreateInScope,
    HandlerInvoker<TContext> Invoke
)
    where TContext : PipelineContext;
