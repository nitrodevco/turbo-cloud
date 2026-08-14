using Orleans;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomSnapshot : RoomInfoSnapshot
{
    [Id(0)]
    public required string Password { get; init; } = string.Empty;

    [Id(1)]
    public required ModSettingsSnapshot ModSettings { get; init; }

    [Id(2)]
    public required ChatSettingsSnapshot ChatSettings { get; init; }

    [Id(3)]
    public required string WorldType { get; init; } = string.Empty;
}
