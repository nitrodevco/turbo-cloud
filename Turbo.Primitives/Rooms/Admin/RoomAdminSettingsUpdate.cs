using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Admin;

[GenerateSerializer, Immutable]
public sealed record RoomAdminSettingsUpdate
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required string Description { get; init; }

    [Id(2)]
    public required RoomDoorModeType DoorMode { get; init; }

    [Id(3)]
    public required string Password { get; init; }

    [Id(4)]
    public required int PlayersMax { get; init; }

    [Id(5)]
    public required ModSettingType WhoCanMute { get; init; }

    [Id(6)]
    public required ModSettingType WhoCanKick { get; init; }

    [Id(7)]
    public required ModSettingType WhoCanBan { get; init; }
}
