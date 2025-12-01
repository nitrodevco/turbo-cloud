using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.NewNavigator;

public record NavigatorAddSavedSearchMessage : IMessageEvent
{
    public string? SearchCode { get; init; }
    public string? Filter { get; init; }
}
