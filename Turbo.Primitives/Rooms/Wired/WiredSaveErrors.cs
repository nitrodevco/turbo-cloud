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

    /// <summary>"Invalid Web API keys": a Web API add-on saved with a key its box did not issue.</summary>
    public const string INVALID_API_KEYS = "wiredfurni.error.invalid_api_keys";

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

    /// <summary>"A username can not be longer than %max_username_length% characters!"</summary>
    public const string USERNAME_LENGTH = "wiredfurni.error.username_length";

    /// <summary>"Can have no more than %max_usernames% usernames!"</summary>
    public const string USERNAMES_COUNT = "wiredfurni.error.usernames_count";

    /// <summary>"You can not capture the same variable twice in one phrase"</summary>
    public const string CAPTURER_DUPLICATE = "wiredfurni.error.capturer.duplicate";

    /// <summary>"Each capturer must be unique in this wired stack"</summary>
    public const string CAPTURE_DESTINATION_CONFLICT = "wiredfurni.error.link.destination_conflict";

    /// <summary>"Can only select counter furni."</summary>
    public const string REQUIRE_COUNTER_FURNI = "wiredfurni.error.require_counter_furni";

    /// <summary>"Placeholder length must be between 1 and 40 characters."</summary>
    public const string PLACEHOLDER_NAME_LENGTH = "wiredfurni.error.placeholder.name_length";

    /// <summary>"Placeholder must only have letters, numbers, or "_" characters."</summary>
    public const string PLACEHOLDER_NAME_SYNTAX = "wiredfurni.error.placeholder.name_syntax";

    /// <summary>"The placeholder trigger_text is reserved for the received message or chat."</summary>
    public const string PLACEHOLDER_RESERVED = "wiredfurni.error.placeholder.reserved";

    /// <summary>"Delimiter can not be empty"</summary>
    public const string PLACEHOLDER_DELIMITER_EMPTY =
        "wiredfurni.error.placeholder.delimiter_empty";

    /// <summary>
    /// "Steady Color Tint only works on BC or temporary furniture. Normal furniture cannot use
    /// this setting."
    /// </summary>
    public const string SET_FURNI_HIGHLIGHT_NORMAL_FURNI =
        "wiredfurni.error.set_furni_highlight.normal_furni";

    /// <summary>A Darken strength the hotel does not offer (no text on Habbo: the key shows).</summary>
    public const string SET_FURNI_HIGHLIGHT_INVALID_STRENGTH =
        "wiredfurni.error.set_furni_highlight.invalid_strength";
}
