using Orleans;

namespace Turbo.Primitives.Plugins.Messages.Snapshots;

/// <summary>One plugin message as a client is told of it (<c>TurboPluginMessagesMessage</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record PluginMessageSnapshot
{
    /// <summary>The message's name, <c>&lt;plugin key&gt;.&lt;name&gt;</c>.</summary>
    [Id(0)]
    public required string Name { get; init; }

    /// <summary>The header the host gave it, the same both ways.</summary>
    [Id(1)]
    public required int Header { get; init; }

    /// <summary>Its fields as compact JSON (<see cref="PluginMessageSchema.Json"/>).</summary>
    [Id(2)]
    public required string Schema { get; init; }
}
