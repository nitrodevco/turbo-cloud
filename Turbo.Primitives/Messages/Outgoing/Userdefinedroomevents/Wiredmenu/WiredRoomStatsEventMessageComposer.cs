using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public sealed record WiredRoomStatsEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
