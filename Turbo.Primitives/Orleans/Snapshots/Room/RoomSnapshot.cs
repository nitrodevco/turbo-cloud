using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

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
