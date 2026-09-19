using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>The items have changed hands; no payload.</summary>
[GenerateSerializer, Immutable]
public sealed record TradingCompletedEventMessageComposer : IComposer;
