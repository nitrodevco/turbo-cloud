namespace Turbo.Primitives.Rooms.Wired.WebApi;

/// <summary>
/// The error codes of the Wired Variables endpoints, answered as <c>{ "error": code }</c> (Habbo's
/// OpenAPI document, <c>components.examples.Wired*Error</c>).
/// </summary>
public static class WiredWebApiErrors
{
    public const string INVALID_TARGET = "wired.variables.invalid_target";
    public const string INVALID_VALUE = "wired.variables.invalid_value";
    public const string KEY_MISSING = "wired.variables.key_missing";
    public const string KEY_INVALID = "wired.variables.key_invalid";
    public const string API_DISABLED = "wired.variables.api_disabled";
    public const string OPERATION_NOT_ALLOWED = "wired.variables.operation_not_allowed";
    public const string USER_NOT_PARTICIPATING = "wired.variables.user_not_participating";
    public const string BULK_DELETE_EMPTY = "wired.variables.bulk_delete_empty";
    public const string BULK_DELETE_INVALID_VARIABLE =
        "wired.variables.bulk_delete_invalid_variable";
    public const string BULK_DELETE_NOT_ENABLED = "wired.variables.bulk_delete_not_enabled";
    public const string BATCH_EMPTY = "wired.variables.batch_empty";
    public const string BATCH_LIMIT_EXCEEDED = "wired.variables.batch_limit_exceeded";
    public const string ROOM_NOT_FOUND = "room.not_found";
    public const string ENTITY_NOT_FOUND = "wired.variables.entity_not_found";
    public const string NOT_FOUND = "wired.variables.not_found";
    public const string TOO_MANY_REQUESTS = "wired.variables.too_many_requests";

    /// <summary>The HTTP status the document answers an error code with.</summary>
    public static int StatusOf(string code) =>
        code switch
        {
            INVALID_TARGET
            or INVALID_VALUE
            or BULK_DELETE_EMPTY
            or BULK_DELETE_INVALID_VARIABLE
            or BATCH_EMPTY
            or BATCH_LIMIT_EXCEEDED => 400,
            KEY_MISSING
            or KEY_INVALID
            or API_DISABLED
            or OPERATION_NOT_ALLOWED
            or USER_NOT_PARTICIPATING
            or BULK_DELETE_NOT_ENABLED => 403,
            ROOM_NOT_FOUND or ENTITY_NOT_FOUND or NOT_FOUND => 404,
            TOO_MANY_REQUESTS => 429,
            _ => 500,
        };
}
