namespace Turbo.Admin.Rooms;

public enum AdminRoomSaveOutcome
{
    Saved,

    /// <summary>There is no such room.</summary>
    NotFound,

    /// <summary>A value is wrong, or the room refused it.</summary>
    Invalid,
}
