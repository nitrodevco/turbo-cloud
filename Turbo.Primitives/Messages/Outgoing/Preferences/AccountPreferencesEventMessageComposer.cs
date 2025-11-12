using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Preferences;

[GenerateSerializer, Immutable]
public sealed record AccountPreferencesEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
