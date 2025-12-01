using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record OpenEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
