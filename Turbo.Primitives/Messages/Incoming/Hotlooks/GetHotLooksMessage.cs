using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Hotlooks;

/// <summary>The avatar editor asks for its hot looks: how many (one byte; Flash asks for 20).</summary>
public record GetHotLooksMessage : IMessageEvent
{
    public required int Amount { get; init; }
}
