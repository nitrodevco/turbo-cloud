namespace Turbo.Primitives.Rooms.Enums;

public enum RoomObjectType
{
    Player = 1,
    Pet = 2,
    OldBot = 3,
    Bot = 4,
}

public static class RoomObjectTypeExtensions
{
    public static string GetString(RoomObjectType objectType) =>
        objectType switch
        {
            RoomObjectType.Pet => "Pet",
            RoomObjectType.OldBot => "Old bot",
            RoomObjectType.Bot => "Bot",
            RoomObjectType.Player => "User",
            _ => throw new System.NotImplementedException(),
        };
}
