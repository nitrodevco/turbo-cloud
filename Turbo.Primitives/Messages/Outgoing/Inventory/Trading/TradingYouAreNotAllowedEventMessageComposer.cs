using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>The player's own account may not trade; no payload.</summary>
[GenerateSerializer, Immutable]
public sealed record TradingYouAreNotAllowedEventMessageComposer : IComposer;
