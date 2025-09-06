using System;
using Turbo.Pipeline.Abstractions.Enums;

namespace Turbo.Pipeline.Core.Configuration;

public class PipelineConfig
{
    public virtual int ChannelCapacity { get; init; } = 10_000;
    public virtual BackpressureMode Backpressure { get; init; } = BackpressureMode.Wait;
    public virtual ExecutionMode Execution { get; init; } = ExecutionMode.Sequential;
    public virtual int DegreeOfParallelism { get; init; } =
        Math.Max(2, Environment.ProcessorCount / 2);
    public virtual bool SwallowHandlerExceptions { get; init; } = false;

    // Metrics hooks
    public virtual Action<object>? OnPublished { get; init; }
    public virtual Action<object, TimeSpan>? OnHandled { get; init; }
    public virtual Action<object, Exception>? OnError { get; init; }
}
