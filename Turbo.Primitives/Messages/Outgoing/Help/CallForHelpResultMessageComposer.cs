using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Help;

[GenerateSerializer, Immutable]
public sealed record CallForHelpResultMessageComposer : IComposer
{
    [Id(0)]
    public required CfhResultSnapshot Result { get; init; }
}
