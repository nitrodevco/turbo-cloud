using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record RedeemVoucherMessage : IMessageEvent
{
    public string? Code { get; init; }
}
