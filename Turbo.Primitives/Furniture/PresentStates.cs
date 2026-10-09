namespace Turbo.Primitives.Furniture;

/// <summary>
/// Present state values as the client renders them: 0 is the wrapped box, 1 its opening. The
/// gift wrapped presents (<c>present_wrap*</c>, Flash's
/// <c>FurnitureGiftWrappedFireworksVisualization</c>) play animation 1 and fire their particle
/// emitter 1 on it, the white glow, sparkles and confetti; a box without that animation simply
/// stays as it is.
/// </summary>
public static class PresentStates
{
    public const int CLOSED = 0;
    public const int OPENING = 1;
}
