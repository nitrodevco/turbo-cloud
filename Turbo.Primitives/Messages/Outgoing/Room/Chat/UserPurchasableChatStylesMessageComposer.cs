using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

/// <summary>
/// The purchasable chat styles (10000 to 99999) the player owns, which the chat style selector
/// offers when <c>custom.chat.styles.enabled</c> (<c>SessionDataManager.onPurchasableChatStyles</c>,
/// parser <c>room/engine/_-I28</c>: a count, then each id).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record UserPurchasableChatStylesMessageComposer : IComposer
{
    [Id(0)]
    public required List<int> ChatStyleIds { get; init; }
}
