namespace Turbo.Primitives.Rooms.Wired;

/// <summary>The texts a wired editor shows when a save is refused (<c>WiredValidationError</c>).</summary>
public static class WiredSaveErrors
{
    /// <summary>This server's own, for a refusal the hotel's texts have no word for.</summary>
    public const string GENERIC = "wired.validation.error";

    /// <summary>"Can only select invisible click furniture."</summary>
    public const string REQUIRE_CLICK_TILES = "wiredfurni.error.require_click_tiles";

    /// <summary>
    /// "Achievement not allowed. Staff needs to configure the enabled achievements in an
    /// Achievement Enabler wired add-on"
    /// </summary>
    public const string ACHIEVEMENT_NOT_ALLOWED = "wiredfurni.error.achievement_not_allowed";
}
