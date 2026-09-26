using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Bots.Snapshots;

/// <summary>
/// A rentable bot as persisted. The inventory holds it while <see cref="RoomId"/> is null; the
/// room owns it once placed and hands it back with whatever its owner changed meanwhile.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record BotSnapshot : IInventoryUnitSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required PlayerId OwnerId { get; init; }

    [Id(2)]
    public required string OwnerName { get; init; }

    [Id(3)]
    public required RoomId? RoomId { get; init; }

    [Id(4)]
    public required string Name { get; init; }

    [Id(5)]
    public required string Motto { get; init; }

    [Id(6)]
    public required string Figure { get; init; }

    [Id(7)]
    public required AvatarGenderType Gender { get; init; }

    [Id(8)]
    public required int X { get; init; }

    [Id(9)]
    public required int Y { get; init; }

    [Id(10)]
    public required Altitude Z { get; init; }

    [Id(11)]
    public required Rotation Rotation { get; init; }

    [Id(12)]
    public required bool FreeRoam { get; init; }

    /// <summary>The chatter editor's text, one line per sentence.</summary>
    [Id(13)]
    public required string ChatText { get; init; }

    [Id(14)]
    public required bool AutoChat { get; init; }

    [Id(15)]
    public required int ChatDelaySeconds { get; init; }

    [Id(16)]
    public required bool MixSentences { get; init; }

    [Id(17)]
    public required AvatarDanceType DanceType { get; init; }
}
