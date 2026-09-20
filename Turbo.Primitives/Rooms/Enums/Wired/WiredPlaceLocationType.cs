namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>Where the "place furni" box puts its furni, as the client's editor numbers the choice.</summary>
public enum WiredPlaceLocationType
{
    /// <summary>Where the source furni stood when the box was saved.</summary>
    SourceLocation = 0,

    /// <summary>At the custom target: a picked furni or a user.</summary>
    CustomLocation = 1,
}
