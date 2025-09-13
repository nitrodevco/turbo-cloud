using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Pipeline.Core.Registry;

public record Behavior(
    Func<IServiceProvider, object, PipelineContext, Func<Task>, CancellationToken, Task> Invoke,
    int Order
);
