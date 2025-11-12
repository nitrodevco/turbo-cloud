using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
