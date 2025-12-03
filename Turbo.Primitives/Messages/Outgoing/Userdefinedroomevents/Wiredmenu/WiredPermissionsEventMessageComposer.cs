using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredPermissionsEventMessageComposer : IComposer
{
    [Id(0)]
    public required bool CanModify { get; init; }

    [Id(1)]
    public required bool CanRead { get; init; }
}
