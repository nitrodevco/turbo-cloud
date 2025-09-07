using System;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Dtos.Players;

public record PlayerSummary(
    long PlayerId,
    string Name,
    string Motto,
    string Figure,
    AvatarGender Gender,
    DateTime CreatedAt
);
