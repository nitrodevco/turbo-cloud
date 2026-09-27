using Orleans;

namespace Turbo.Primitives.Players.Snapshots;

/// <summary>One row of <c>player_chat_styles</c>: a bubble the client can draw and who may use it.</summary>
[GenerateSerializer, Immutable]
public sealed record ChatStyleSnapshot
{
    [Id(0)]
    public required int ClientStyleId { get; init; }

    [Id(1)]
    public required bool ClubOnly { get; init; }

    [Id(2)]
    public required bool AmbassadorOnly { get; init; }

    [Id(3)]
    public required bool StaffOnly { get; init; }

    [Id(4)]
    public required bool Purchasable { get; init; }

    [Id(5)]
    public required bool System { get; init; }
}
