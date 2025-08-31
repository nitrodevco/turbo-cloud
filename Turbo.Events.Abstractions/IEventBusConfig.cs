using System;
using Turbo.Events.Abstractions.Enums;

namespace Turbo.Events.Abstractions;

public class IEventBusConfig
{
    public ExecutionModeType Execution { get; set; } = ExecutionModeType.Sequential;
    public int DegreeOfParallelism { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
    public Func<object, string?>? PartitionKey { get; set; }
    public int ChannelCapacity { get; set; } = 10000;
    public BackpressureModeType Backpressure { get; set; } = BackpressureModeType.Wait;
    public bool SwallowHandlerExceptions { get; set; } = false;
    public Action<object>? OnPublished { get; set; }
    public Action<object, TimeSpan>? OnHandled { get; set; }
    public Action<object, Exception>? OnError { get; set; }
}
