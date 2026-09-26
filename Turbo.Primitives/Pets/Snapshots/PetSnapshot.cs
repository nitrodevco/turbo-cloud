using System;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>
/// A pet as persisted. The inventory holds it while <see cref="RoomId"/> is null; a room owns it
/// (and its live stats) once placed, and hands it back with the stats it accumulated.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PetSnapshot : IInventoryUnitSnapshot
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
    public required PetFigureSnapshot Figure { get; init; }

    [Id(6)]
    public required int Level { get; init; }

    [Id(7)]
    public required int Experience { get; init; }

    [Id(8)]
    public required int Energy { get; init; }

    /// <summary>Shown by the client as happiness.</summary>
    [Id(9)]
    public required int Nutrition { get; init; }

    [Id(10)]
    public required int Respect { get; init; }

    [Id(11)]
    public required int RarityLevel { get; init; }

    [Id(12)]
    public required bool HasSaddle { get; init; }

    [Id(13)]
    public required bool AnyoneCanRide { get; init; }

    [Id(14)]
    public required bool HasBreedingPermission { get; init; }

    [Id(15)]
    public required int X { get; init; }

    [Id(16)]
    public required int Y { get; init; }

    [Id(17)]
    public required Altitude Z { get; init; }

    [Id(18)]
    public required Rotation Rotation { get; init; }

    [Id(19)]
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>When a monsterplant was last watered; its well-being counts down from here.</summary>
    [Id(20)]
    public required DateTime WateredAtUtc { get; init; }

    [Id(21)]
    public required DateTime? HarvestedAtUtc { get; init; }

    public int TypeId => Figure.TypeId;
    public int BreedId => Figure.BreedId;
    public bool IsMonsterplant => PetTypes.IsMonsterplant(Figure.TypeId);
}
