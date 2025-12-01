using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredErrorLogsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
