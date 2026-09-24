using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// Everything the badge editor may pick from. The client asks once and keeps it for the rest of
/// the session.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildEditorDataMessageComposer : IComposer
{
    [Id(0)]
    public required GuildEditorDataSnapshot EditorData { get; init; }
}
