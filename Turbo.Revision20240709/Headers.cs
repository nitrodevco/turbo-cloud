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
    public const int InitDiffieHandshakeComposer = 771;
    public const int CompleteDiffieHandshakeComposer = 3777;
}
