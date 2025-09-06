using System;
using System.Threading.Channels;
using Turbo.Pipeline.Abstractions.Enums;

namespace Turbo.Pipeline.Core.Configuration;

public class PipelineConfig
{
    public virtual int ChannelCapacity { get; init; } = 10_000;
    public virtual BoundedChannelFullMode Backpressure { get; init; } = BoundedChannelFullMode.Wait;
    public virtual ExecutionMode Execution { get; init; } = ExecutionMode.Parallel;
    public virtual int DegreeOfParallelism { get; init; } =
        Math.Max(2, Environment.ProcessorCount / 2);
}
