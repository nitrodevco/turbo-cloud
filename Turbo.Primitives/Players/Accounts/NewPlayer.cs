using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Accounts;

/// <summary>A player to create: their name, motto, gender, and figure (null for the gender's default).</summary>
public sealed record NewPlayer(string Name, string? Motto, AvatarGenderType Gender, string? Figure);
