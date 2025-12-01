using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredRoomSettingsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
