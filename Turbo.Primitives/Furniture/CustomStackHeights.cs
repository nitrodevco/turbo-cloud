namespace Turbo.Primitives.Furniture;

/// <summary>Stack tile heights as the client sends them: hundredths of a tile.</summary>
public static class CustomStackHeights
{
    /// <summary>Sent for the "above stack" button: rest on top of whatever is on the tile.</summary>
    public const int ABOVE_STACK = -100;
    public const int UNITS_PER_TILE = 100;
}
