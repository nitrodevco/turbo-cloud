namespace Turbo.Rooms.Mapping;

internal sealed class Node
{
    public int X;
    public int Y;
    public int G; // Cost from start
    public int H; // Heuristic cost to goal
    public int F => G + H;
    public Node? Parent;
}
