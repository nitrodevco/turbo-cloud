using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>GetCfhChatlogMessageComposer(issueId)</c>.</summary>
public record GetCfhChatlogMessage : IMessageEvent
{
    public required int IssueId { get; init; }
}
