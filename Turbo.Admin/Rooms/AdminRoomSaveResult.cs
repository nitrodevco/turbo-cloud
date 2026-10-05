namespace Turbo.Admin.Rooms;

/// <summary>What came of saving a room's settings from the panel, and why not when it did not.</summary>
public sealed record AdminRoomSaveResult(AdminRoomSaveOutcome Outcome, string Message)
{
    public static AdminRoomSaveResult Saved { get; } =
        new(AdminRoomSaveOutcome.Saved, "Settings saved.");

    public static AdminRoomSaveResult Invalid(string message) =>
        new(AdminRoomSaveOutcome.Invalid, message);
}
