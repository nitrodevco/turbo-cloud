namespace Turbo.Primitives.Rooms.Enums.Wired;

public static class WiredAvailabilityTypeExtensions
{
    /// <summary>
    /// Whether a variable keeps its values when the room unloads: "Permanent" and "Permanent,
    /// shared" (<c>wiredfurni.params.variables.availability.10</c> / <c>.11</c>). A shared
    /// variable is a permanent one other rooms may also use.
    /// </summary>
    public static bool IsPermanent(this WiredAvailabilityType availability) =>
        availability is WiredAvailabilityType.Persistent or WiredAvailabilityType.Shared;
}
