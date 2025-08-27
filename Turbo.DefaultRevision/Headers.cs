namespace Turbo.DefaultRevision;

public enum MessageEvent
{
    ClientHelloMessageEvent = 4000,
    InitDiffieHandshakeMessageEvent = 586,
}

public static class MessageComposer
{
    public const int InitDiffieHandshakeComposer = 771;
    public const int CompleteDiffieHandshakeComposer = 3777;
}
