using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public sealed record WiredFurniSelectorEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
