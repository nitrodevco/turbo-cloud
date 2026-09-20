namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>What clicking furni does while a wired click setting is on, as the client's <c>WiredEnvironment</c> numbers it.</summary>
public enum WiredClickFurniType
{
    Default = 0,

    /// <summary>Furni cannot be clicked: the click lands on the tile under it.</summary>
    PassThrough = 1,
}
