using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>Both sides accepted; the client starts its confirmation countdown. No payload.</summary>
[GenerateSerializer, Immutable]
public sealed record TradingConfirmationEventMessageComposer : IComposer;
