using System.Collections.Immutable;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Why a player's security level is what it is, and what it makes the client offer that the
/// server will refuse. The client reads the level as a threshold, so a player given one node that
/// needs level 7 is also shown everything that needs 7 or less. For the console; see
/// <see cref="PermissionProjection.ReportLevel"/>.
/// </summary>
/// <param name="Level">The level the client is sent.</param>
/// <param name="Source">
/// What set it: the held node with the highest client level, or the meta key when its floor is
/// higher. Null at level none.
/// </param>
/// <param name="ShownButRefused">
/// Registered nodes whose client level is at or below <paramref name="Level"/> that the player does
/// not hold: features the client will draw and the server will turn down. Highest level first.
/// </param>
public sealed record PermissionLevelReport(
    SecurityLevelType Level,
    string? Source,
    ImmutableArray<PermissionNodeDefinition> ShownButRefused
);
