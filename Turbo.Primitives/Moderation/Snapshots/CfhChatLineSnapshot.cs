using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>A chat line the reporter picked to go with a call for help: who said it and what.</summary>
[GenerateSerializer, Immutable]
public sealed record CfhChatLineSnapshot
{
    [Id(0)]
    public required int PlayerId { get; init; }

    [Id(1)]
    public required string Text { get; init; }
}
