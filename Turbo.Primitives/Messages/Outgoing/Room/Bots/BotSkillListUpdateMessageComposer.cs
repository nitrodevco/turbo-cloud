using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Bots;

[GenerateSerializer, Immutable]
public sealed record BotSkillListUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required int BotId { get; init; }

    [Id(1)]
    public required ImmutableArray<BotSkillSnapshot> Skills { get; init; }
}
