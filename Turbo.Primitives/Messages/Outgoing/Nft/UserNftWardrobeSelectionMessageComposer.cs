using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Nft;

/// <summary>
/// The NFT outfit the player wears, by token id, and the look the avatar editor falls back to on
/// its other tabs (Flash <c>HabboAvatarEditor.loadFallbackFigure</c>, skipped while it is empty).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record UserNftWardrobeSelectionMessageComposer : IComposer
{
    [Id(0)]
    public required string CurrentTokenId { get; init; }

    [Id(1)]
    public required string FallbackFigure { get; init; }

    [Id(2)]
    public required string FallbackGender { get; init; }
}
