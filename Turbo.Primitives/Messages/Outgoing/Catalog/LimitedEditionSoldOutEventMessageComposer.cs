using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// The limited series is sold out: the client alerts "Sold Out! This Limited Edition Rare is
/// already sold out. Be quicker next time!". It carries nothing.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LimitedEditionSoldOutEventMessageComposer : IComposer;
