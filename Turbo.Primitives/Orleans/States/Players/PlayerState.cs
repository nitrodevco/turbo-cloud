using System;
using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Orleans.States.Players;

[GenerateSerializer]
public sealed class PlayerState
{
    [Id(0)]
    public required string Name { get; set; } = string.Empty;

    [Id(1)]
    public required string Motto { get; set; } = string.Empty;

    [Id(2)]
    public required string Figure { get; set; } = string.Empty;

    [Id(3)]
    public required AvatarGenderEnum Gender { get; set; } = AvatarGenderEnum.Male;

    [Id(4)]
    public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Id(5)]
    public required bool IsLoaded { get; set; } = false;

    [Id(6)]
    public required DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
