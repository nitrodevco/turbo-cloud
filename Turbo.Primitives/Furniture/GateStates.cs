namespace Turbo.Primitives.Furniture;

/// <summary>
/// Gate state values as the client renders them: 0 is shut and blocks the tile, 1 is open and
/// lets an avatar through. Both the plain gate and the one-way gate are drawn from this table.
/// </summary>
public static class GateStates
{
    public const int CLOSED = 0;
    public const int OPEN = 1;
}
