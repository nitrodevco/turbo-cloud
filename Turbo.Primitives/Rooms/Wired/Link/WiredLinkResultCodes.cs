namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>
/// The outcome of a WIRED Link request. The client shows the text key
/// <c>wiredlink.error.&lt;code&gt;</c> for any code but <see cref="OK"/>
/// (<c>WiredLinkView.operationFailed</c>; the hotel texts name each one).
/// </summary>
public static class WiredLinkResultCodes
{
    public const int OK = 0;

    /// <summary>"Only the furniture owner can configure this Link."</summary>
    public const int NOT_OWNER = 1;

    /// <summary>"This network no longer exists or does not belong to you."</summary>
    public const int NETWORK_NOT_FOUND = 2;

    /// <summary>"Enter a network name containing text, with no more than 255 characters."</summary>
    public const int INVALID_NAME = 3;

    /// <summary>"You already have a network with that name. Please choose another."</summary>
    public const int DUPLICATE_NAME = 4;

    /// <summary>"Another Link is already connected to this network in this room."</summary>
    public const int NETWORK_IN_USE_IN_ROOM = 5;

    /// <summary>"The network identifier is invalid. Close and reopen this Link to refresh it."</summary>
    public const int INVALID_NETWORK_ID = 6;

    /// <summary>"This Link is no longer available in the room."</summary>
    public const int LINK_UNAVAILABLE = 7;

    /// <summary>"You have reached the network limit for your account."</summary>
    public const int NETWORK_LIMIT = 8;
}
