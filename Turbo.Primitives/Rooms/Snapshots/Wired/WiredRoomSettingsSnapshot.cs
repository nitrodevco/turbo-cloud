using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public sealed record WiredRoomSettingsSnapshot
{
    [Id(0)]
    public required WiredPermissionFlags ModifyPermissionMask { get; init; }

    [Id(1)]
    public required WiredPermissionFlags ReadPermissionMask { get; init; }

    [Id(2)]
    public required string Timezone { get; init; }
}
