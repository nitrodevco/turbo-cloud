using System;
using Orleans.Streams;

namespace Turbo.Streams;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AutoStreamProviderAttribute(
    string name,
    int queueCount = -1,
    StreamPubSubType streamPubSubType = StreamPubSubType.ExplicitGrainBasedOnly
) : Attribute
{
    public string Name { get; } = name;

    public int QueueCount { get; } = queueCount;

    public StreamPubSubType StreamPubSubType { get; } = streamPubSubType;
}
