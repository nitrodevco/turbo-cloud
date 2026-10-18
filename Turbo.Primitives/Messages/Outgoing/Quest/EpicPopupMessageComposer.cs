using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>
/// The friend bar's epic popup: a window showing one picture
/// (<c>HabboEpicPopupView.showPopup</c>, AS3 WIN63-202610151745).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record EpicPopupMessageComposer : IComposer
{
    /// <summary>The picture's address (<c>content_static_bitmap.assetUri</c>).</summary>
    [Id(0)]
    public required string ImageUri { get; init; }
}
