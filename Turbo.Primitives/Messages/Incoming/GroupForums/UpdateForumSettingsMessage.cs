using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

public record UpdateForumSettingsMessage : IMessageEvent
{
    public required int GroupId { get; init; }

    public required int ReadPermission { get; init; }

    public required int PostMessagePermission { get; init; }

    public required int PostThreadPermission { get; init; }

    public required int ModeratePermission { get; init; }
}
