namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// Why a room settings save was rejected. The values are the error codes the client maps to its
/// own messages; anything it does not know is shown as a generic failure.
/// </summary>
public enum RoomSettingsSaveErrorType
{
    None = 0,

    /// <summary>The save was malformed; the client shows a generic failure.</summary>
    Invalid = 1,
    PasswordRequired = 5,
    NameRequired = 7,
    NameNotAllowed = 8,
    DescriptionNotAllowed = 10,
    TagNotAllowed = 11,
    TagNotChoosable = 12,
    TagTooLong = 13,
}
