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

    /// <summary>
    /// "Placements are not allowed for the selected furni type. You can only select Builders Club
    /// furniture that do not have advanced functionality."
    /// </summary>
    public const string FURNI_CAN_NOT_BE_CLONED = "wiredfurni.error.furni_can_not_be_cloned";

    /// <summary>"Variable name length must be between 1 and 40 characters."</summary>
    public const string VARIABLE_NAME_LENGTH = "wiredfurni.error.variables.name_length";

    /// <summary>"Variable name must only have letters, numbers, or "_" characters."</summary>
    public const string VARIABLE_NAME_SYNTAX = "wiredfurni.error.variables.name_syntax";

    /// <summary>"Variable name is already in use in this room, please choose another one!"</summary>
    public const string VARIABLE_NAME_UNIQUE = "wiredfurni.error.variables.name_uniq";

    /// <summary>"This room reached the limit of permanent variables for this variable type."</summary>
    public const string VARIABLE_PERMANENT_LIMIT = "wiredfurni.error.variables.permanent_limit";
}
