namespace Turbo.Primitives.Rooms.Enums;

public enum RoomPropertyType
{
    Floor = 1,
    Wall = 2,
    Landscape = 3,
    LandscapeAnimated = 4,
}

public static class RoomPropertyTypeExtensions
{
    public static string GetString(RoomPropertyType objectType) =>
        objectType switch
        {
            RoomPropertyType.Floor => "floor",
            RoomPropertyType.Wall => "wallpaper",
            RoomPropertyType.Landscape => "landscape",
            RoomPropertyType.LandscapeAnimated => "landscapeanim",
            _ => throw new System.NotImplementedException(),
        };
}
