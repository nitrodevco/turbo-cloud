using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Opens a WIRED Link's window for its owner, who double-clicked it; the client then asks for
/// the Link's configuration (AS3 <c>WiredLinkController.onOpen</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkOpenMessageComposer : IComposer
{
    [Id(0)]
    public required int FurniId { get; init; }
}
