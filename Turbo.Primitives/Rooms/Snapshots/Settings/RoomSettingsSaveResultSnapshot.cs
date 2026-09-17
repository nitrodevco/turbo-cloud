using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

[GenerateSerializer, Immutable]
public sealed record RoomSettingsSaveResultSnapshot
{
    [Id(0)]
    public required RoomSettingsSaveErrorType Error { get; init; }

    /// <summary>The offending value (currently only the rejected tag), shown by the client.</summary>
    [Id(1)]
    public string Info { get; init; } = string.Empty;

    public bool Succeeded => Error == RoomSettingsSaveErrorType.None;

    public static RoomSettingsSaveResultSnapshot Success { get; } =
        new() { Error = RoomSettingsSaveErrorType.None };

    public static RoomSettingsSaveResultSnapshot Failed(
        RoomSettingsSaveErrorType error,
        string info = ""
    ) => new() { Error = error, Info = info };
}
