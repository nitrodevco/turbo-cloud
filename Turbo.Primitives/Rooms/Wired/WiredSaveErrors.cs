namespace Turbo.Primitives.Rooms.Wired;

/// <summary>The texts a wired editor shows when a save is refused (<c>WiredValidationError</c>).</summary>
public static class WiredSaveErrors
{
    /// <summary>This server's own, for a refusal the hotel's texts have no word for.</summary>
    public const string GENERIC = "wired.validation.error";

    /// <summary>
    /// A furni source the slot does not offer. The hotel texts have no entry for it, so Habbo's
    /// editor shows the raw key (official client capture 2026-10-20).
    /// </summary>
    public const string INVALID_FURNI_SOURCE = "wiredfurni.error.invalid_furni_source";

    /// <summary>A user source the slot does not offer (official client capture 2026-10-22).</summary>
    public const string INVALID_USER_SOURCE = "wiredfurni.error.invalid_user_source";

    /// <summary>"Can only select WIRED Conditions and Add-Ons!": Condition Evaluation's picks.</summary>
    public const string CONDITION_EVALUATION_FURNI = "wiredfurni.error.condition_evaluation_furni";

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

    /// <summary>
    /// Set Visibility saved with a transparency other than 0 or 100. The hotel texts have no
    /// entry for it, so Habbo's editor shows the raw key (official client capture 2026-10-20).
    /// </summary>
    public const string SET_FURNI_TRANSPARENCY_INVALID_TRANSPARENCY =
        "wiredfurni.error.set_furni_transparency.invalid_transparency";

    /// <summary>"Variable name length must be between 1 and 40 characters."</summary>
    public const string VARIABLE_NAME_LENGTH = "wiredfurni.error.variables.name_length";

    /// <summary>"Variable name must only have letters, numbers, or "_" characters."</summary>
    public const string VARIABLE_NAME_SYNTAX = "wiredfurni.error.variables.name_syntax";

    /// <summary>"Variable name is already in use in this room, please choose another one!"</summary>
    public const string VARIABLE_NAME_UNIQUE = "wiredfurni.error.variables.name_uniq";

    /// <summary>"This room reached the limit of permanent variables for this variable type."</summary>
    public const string VARIABLE_PERMANENT_LIMIT = "wiredfurni.error.variables.permanent_limit";

    /// <summary>
    /// "Only the owner of this wired variable or room owner has the permission to make this change."
    /// </summary>
    public const string VARIABLE_OWNER_OR_ROOM_OWNER_ONLY =
        "wiredfurni.error.variables.permissions.2";

    /// <summary>"Invalid availability": one the box's editor does not offer.</summary>
    public const string VARIABLE_INVALID_AVAILABILITY =
        "wiredfurni.error.variables.invalid_availability";

    /// <summary>"You can only set up shared variables in your own room."</summary>
    public const string VARIABLE_SHARED_OWN_ROOM = "wiredfurni.error.variables.shared_var_own_room";

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

    /// <summary>"Can only select antenna furni."</summary>
    public const string REQUIRE_ANTENNA_FURNI = "wiredfurni.error.require_antenna_furni";

    /// <summary>
    /// "Can only select chests." with the note that WIRED Effects need a Wired chest, not a
    /// normal one.
    /// </summary>
    public const string REQUIRE_CHESTS = "wiredfurni.error.require_chests";

    /// <summary>"Can only select credit chests." with the same note.</summary>
    public const string REQUIRE_COIN_CHESTS = "wiredfurni.error.require_coin_chests";

    /// <summary>"Can only select furni chests." with the same note.</summary>
    public const string REQUIRE_FURNI_CHESTS = "wiredfurni.error.require_furni_chests";

    /// <summary>"Misconfigured contract": a custom contract that neither asks nor gives anything.</summary>
    public const string MISCONFIGURED_CONTRACT = "wiredfurni.error.misconfigured_contract";

    /// <summary>"Can only select contracts!"</summary>
    public const string REQUIRE_CONTRACTS = "wiredfurni.error.require_contracts";

    /// <summary>
    /// "Can only select contracts! You can also select the WIRED Effect if no contract was
    /// involved in the transaction"
    /// </summary>
    public const string REQUIRE_CONTRACTS_OR_TRADE_WIRED =
        "wiredfurni.error.require_contracts_or_trade_wired";

    /// <summary>"Can only select wired room linkers!"</summary>
    public const string REQUIRE_ROOM_LINKER = "wiredfurni.error.require_room_linker";

    /// <summary>"Can not select temporary furni in Wired."</summary>
    public const string TEMP_FURNI_NOT_ALLOWED = "wiredfurni.error.temp_furni_not_allowed";

    /// <summary>"Chest name is not allowed"</summary>
    public const string UNACCEPTABLE_CHEST_NAME = "wiredfurni.error.unacceptable_chest_name";

    /// <summary>"Chest description is not allowed"</summary>
    public const string UNACCEPTABLE_CHEST_DESC = "wiredfurni.error.unacceptable_chest_desc";

    /// <summary>"The following products/badges do not exist: %products%"</summary>
    public const string INVALID_REWARD_PRODUCTS = "wiredfurni.error.invalid_reward_products";

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
