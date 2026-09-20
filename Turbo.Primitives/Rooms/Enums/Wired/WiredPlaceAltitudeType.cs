namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>How high the "place furni" box puts its furni, as the client's editor numbers the choice.</summary>
public enum WiredPlaceAltitudeType
{
    OnTopOfTargetLocation = 0,

    /// <summary>The altitude the source furni had when the box was saved.</summary>
    SourceAltitude = 1,

    /// <summary>The altitude of the custom target.</summary>
    CustomAltitude = 2,
}
