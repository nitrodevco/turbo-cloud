using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>An offer arrived for a trade that is not open; no payload.</summary>
[GenerateSerializer, Immutable]
public sealed record TradingNotOpenEventMessageComposer : IComposer;
