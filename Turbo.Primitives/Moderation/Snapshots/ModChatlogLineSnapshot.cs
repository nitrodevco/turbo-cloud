using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>A chatlog line: the time as shown, who said it (0 a bot or pet) and what.</summary>
[GenerateSerializer, Immutable]
public sealed record ModChatlogLineSnapshot
{
    [Id(0)]
    public required string Time { get; init; }

    [Id(1)]
    public required int ChatterId { get; init; }

    [Id(2)]
    public required string ChatterName { get; init; }

    [Id(3)]
    public required string Message { get; init; }

    [Id(4)]
    public required bool Highlighted { get; init; }
}
