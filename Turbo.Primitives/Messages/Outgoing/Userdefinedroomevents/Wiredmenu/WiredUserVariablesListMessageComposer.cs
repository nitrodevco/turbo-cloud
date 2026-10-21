using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredUserVariablesListMessageComposer : IComposer
{
    [Id(0)]
    public required WiredVariableOwnersPageSnapshot Page { get; init; }
}
