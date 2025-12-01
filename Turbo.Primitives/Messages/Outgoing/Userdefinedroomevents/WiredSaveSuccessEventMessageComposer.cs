using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record WiredSaveSuccessEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
