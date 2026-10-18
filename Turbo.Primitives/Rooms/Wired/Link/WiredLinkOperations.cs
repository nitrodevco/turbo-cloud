namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>
/// Which request a WiredLinkOperationResultMessageComposer answers, as the Room Linker's link
/// window tells them apart (<c>WiredLinkView.operationFailed</c> / <c>operationSucceeded</c>).
/// </summary>
public static class WiredLinkOperations
{
    /// <summary>The window asking for its linker's configuration, when it opens.</summary>
    public const int GET_CONFIGURATION = 0;

    public const int SAVE = 1;
    public const int CREATE_NETWORK = 2;
    public const int RENAME_NETWORK = 3;
    public const int DELETE_NETWORK = 4;
    public const int GET_CONNECTIONS = 5;
}
