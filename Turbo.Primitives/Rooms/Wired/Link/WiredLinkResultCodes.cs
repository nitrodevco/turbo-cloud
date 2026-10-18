namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>
/// The outcome of a Room Linker link request. The client shows the text key
/// <c>wiredlink.error.&lt;code&gt;</c> for any code but <see cref="OK"/>.
/// </summary>
public static class WiredLinkResultCodes
{
    public const int OK = 0;

    /// <summary>"This Link is no longer available in the room."</summary>
    public const int LINK_UNAVAILABLE = 7;
}
