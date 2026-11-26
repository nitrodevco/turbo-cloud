using Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>What asking to preview a group came to, and why when it was refused.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionPreviewResultSnapshot
{
    [Id(0)]
    public required PermissionPreviewResultType Result { get; init; }

    /// <summary>
    /// For <see cref="PermissionPreviewResultType.NodeNotHeld"/>: the first node, in ordinal order,
    /// that the group would give and the player does not hold.
    /// </summary>
    [Id(1)]
    public string? Node { get; init; }

    /// <summary>For <see cref="PermissionPreviewResultType.Started"/>: the preview now running.</summary>
    [Id(2)]
    public PermissionPreviewSnapshot? Preview { get; init; }
}
