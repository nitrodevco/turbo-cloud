using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Bots.Snapshots;

/// <summary>
/// A placed bot as staff set it from the admin panel: everything its owner's skills could set,
/// written at once.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record BotStaffEditSnapshot
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required string Motto { get; init; }

    [Id(2)]
    public required string Figure { get; init; }

    [Id(3)]
    public required AvatarGenderType Gender { get; init; }

    /// <summary>The lines it says, one a line.</summary>
    [Id(4)]
    public required string ChatText { get; init; }

    [Id(5)]
    public required bool AutoChat { get; init; }

    [Id(6)]
    public required int ChatDelaySeconds { get; init; }

    [Id(7)]
    public required bool MixSentences { get; init; }

    [Id(8)]
    public required bool FreeRoam { get; init; }

    [Id(9)]
    public required AvatarDanceType Dance { get; init; }
}
