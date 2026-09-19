using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>The other party's account may not trade; no payload.</summary>
[GenerateSerializer, Immutable]
public sealed record TradingOtherNotAllowedEventMessageComposer : IComposer;
