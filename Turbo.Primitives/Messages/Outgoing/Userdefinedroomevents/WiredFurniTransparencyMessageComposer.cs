using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Fades furni in or out for the players it is sent to (WIRED Effect: Set Visibility on furni). Flash
/// <c>WiredFurniTransparencyMessageParser</c>: the furni ids, the transparency (0 visible to 100
/// invisible), whether clicks pass through, and the easing of the 500 ms fade
/// (<c>FurniTransparencyEasingMode</c>). The client applies it to a furni not built yet once it is.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredFurniTransparencyMessageComposer : IComposer
{
    /// <summary>As wired names furni to the client: a wall item by its id made negative.</summary>
    [Id(0)]
    public required ImmutableArray<int> FurniIds { get; init; }

    [Id(1)]
    public required int Transparency { get; init; }

    [Id(2)]
    public required bool AllowClickThrough { get; init; }

    [Id(3)]
    public required int EasingMode { get; init; }
}
