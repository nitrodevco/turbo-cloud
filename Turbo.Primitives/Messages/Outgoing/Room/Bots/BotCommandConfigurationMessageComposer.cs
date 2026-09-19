using Orleans;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Bots;

[GenerateSerializer, Immutable]
public sealed record BotCommandConfigurationMessageComposer : IComposer
{
    [Id(0)]
    public required int BotId { get; init; }

    [Id(1)]
    public required BotSkillType Skill { get; init; }

    [Id(2)]
    public required string Data { get; init; }
}
