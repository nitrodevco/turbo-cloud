using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

/// <summary>
/// The unread forums poll, every groupforum.poll.period seconds (GroupForumController.as:236-239, 793).
/// </summary>
public record GetUnreadForumsCountMessage : IClientTimerMessage;
