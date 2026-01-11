using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredAllVariableHoldersEventMessageComposer : IComposer
{
    [Id(0)]
    public required WiredVariableSnapshot VariableSnapshot { get; init; }
}
