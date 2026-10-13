using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

/// <summary>A Habbicon sent in a console conversation (<c>MainView.onHabbiconSelected</c>).</summary>
public record SendHabbiconMessage : IMessageEvent
{
    public required int ChatId { get; init; }

    public required int HabbiconId { get; init; }

    public required int ConfirmationId { get; init; }
}
