using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

/// <summary>The value one fx shows on one avatar or furni right now.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableFxStatusSnapshot
{
    [Id(0)]
    public required VariableFxStatusKeySnapshot Key { get; init; }

    /// <summary>
    /// Set when the viewer is being brought up to date rather than told of a change: the client
    /// then neither animates the value nor counts it as a change that shows the fx.
    /// </summary>
    [Id(1)]
    public required bool IsInitialize { get; init; }

    [Id(2)]
    public required long Value { get; init; }

    /// <summary>The range for this entity when it differs from the config's default; both or neither.</summary>
    [Id(3)]
    public required long? OverrideMinValue { get; init; }

    [Id(4)]
    public required long? OverrideMaxValue { get; init; }

    [Id(5)]
    public required ImmutableDictionary<string, string> Extra { get; init; }
}
