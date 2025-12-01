using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record RedeemVoucherMessage : IMessageEvent
{
    public string? Code { get; init; }
}
