namespace Turbo.Revision20240709;

public enum MessageEvent
{
    CompleteDiffieHandshakeMessageEvent = 2616,
    DisconnectMessageEvent = 1113,
    InfoRetrieveMessageEvent = 245,
    InitDiffieHandshakeMessageEvent = 586,
    SSOTicketMessageEvent = 53,
    UniqueIDMessageEvent = 1390,
    VersionCheckMessageEvent = 2602,
}

public static class MessageComposer
{
    // Handshake
    public const int AuthenticationOKMessageComposer = 2323;
    public const int CompleteDiffieHandshakeComposer = 3777;
    public const int InitDiffieHandshakeComposer = 771;
    public const int IsFirstLoginOfDayComposer = 3129;
    public const int NoobnessLevelMessageComposer = 1916;
    public const int UserObjectComposer = 3048;
    public const int UserRightsMessageComposer = 1416;

    // Users
    public const int ScrSendUserInfoComposer = 937;
}
