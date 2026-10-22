using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Help;

/// <summary>
/// A moderator's reply to a player's call for help (<c>CallForHelpManager.onCallForHelpReply</c>),
/// shown under the client's own <c>help.cfh.reply.title</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CallForHelpReplyMessageComposer : IComposer
{
    [Id(0)]
    public required string Message { get; init; }
}
