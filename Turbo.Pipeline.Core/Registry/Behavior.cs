using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Core.Registry;

public record Behavior<TContext>(
    string OwnerId,
    Type MessageType,
    int Order,
    bool UseAmbientScope,
    Func<IServiceScope>? BeginScope,
    Func<IServiceProvider, object> CreateInScope,
    BehaviorInvoker<TContext> Invoke
)
    where TContext : PipelineContext;
