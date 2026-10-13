using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// What <c>ModeratorInit</c> gives the tool (<c>ModeratorInitData</c>): the message templates and
/// which actions it offers. Each flag is a permission node the player holds.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorInitSnapshot
{
    [Id(0)]
    public required ImmutableArray<string> MessageTemplates { get; init; }

    [Id(1)]
    public required ImmutableArray<string> RoomMessageTemplates { get; init; }

    [Id(2)]
    public required bool CfhPermission { get; init; }

    [Id(3)]
    public required bool ChatlogsPermission { get; init; }

    [Id(4)]
    public required bool AlertPermission { get; init; }

    [Id(5)]
    public required bool KickPermission { get; init; }

    [Id(6)]
    public required bool BanPermission { get; init; }

    [Id(7)]
    public required bool RoomAlertPermission { get; init; }

    [Id(8)]
    public required bool RoomKickPermission { get; init; }
}
