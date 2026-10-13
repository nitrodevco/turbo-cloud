using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>Whether a moderation action on a player went through: on success the client asks for the player's info again, else it alerts (<c>ModerationMessageHandler.onModeratorActionResult</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorActionResultMessageComposer : IComposer
{
    [Id(0)]
    public required int UserId { get; init; }

    [Id(1)]
    public required bool Success { get; init; }
}
