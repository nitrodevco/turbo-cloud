using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Friendfurni;

[GenerateSerializer, Immutable]
public sealed record FriendFurniStartConfirmationMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
