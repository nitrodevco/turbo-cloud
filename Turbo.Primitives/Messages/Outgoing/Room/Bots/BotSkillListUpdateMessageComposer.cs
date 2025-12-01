using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Bots;

[GenerateSerializer, Immutable]
public sealed record BotSkillListUpdateMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
