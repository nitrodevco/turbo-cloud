using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>GetRoomVisitsMessageComposer(userId)</c>.</summary>
public record GetRoomVisitsMessage : IMessageEvent
{
    public required int UserId { get; init; }
}
