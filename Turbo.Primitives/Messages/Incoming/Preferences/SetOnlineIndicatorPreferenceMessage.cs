using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetOnlineIndicatorPreferenceMessage : IMessageEvent
{
    /// <summary>The drop menu's selection; see <c>OnlineIndicatorPreferenceType</c>.</summary>
    public required int Selection { get; init; }
}
