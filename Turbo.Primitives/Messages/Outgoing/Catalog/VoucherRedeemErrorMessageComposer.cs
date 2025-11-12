using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record VoucherRedeemErrorMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
