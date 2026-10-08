namespace Turbo.Primitives.Furniture;

/// <summary>
/// The state a jukebox or a sound machine shows: 1 is playing. The client starts its music
/// player for the room when it sees 1 and stops it at 0
/// (<c>FurnitureJukeboxLogic</c>, <c>FurnitureSoundMachineLogic</c>).
/// </summary>
public static class JukeboxStates
{
    public const int OFF = 0;
    public const int ON = 1;
}
