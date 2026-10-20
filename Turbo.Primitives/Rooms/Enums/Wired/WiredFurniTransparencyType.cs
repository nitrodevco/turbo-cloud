namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// What Set Visibility makes its targets: the transparency the client draws them with, 0 to 100.
/// The editor offers only these two (<c>wiredfurni.params.set_furni_transparency.visible</c> /
/// <c>.invisible</c>), and Habbo refuses any other value
/// (<c>wiredfurni.error.set_furni_transparency.invalid_transparency</c>).
/// </summary>
public enum WiredFurniTransparencyType
{
    Visible = 0,
    Invisible = 100,
}
