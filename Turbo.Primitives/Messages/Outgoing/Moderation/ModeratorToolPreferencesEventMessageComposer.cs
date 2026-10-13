using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>Where the moderator left the issue handler (<c>IssueManager.setToolPreferences</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorToolPreferencesEventMessageComposer : IComposer
{
    [Id(0)]
    public required ModToolPreferencesSnapshot Preferences { get; init; }
}
