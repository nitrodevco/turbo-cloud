using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Badges;

public record SetActivatedBadgesMessage : IMessageEvent
{
    /// <summary>The badge code per slot, in slot order; an empty code is an empty slot.</summary>
    public required List<string> BadgeCodes { get; init; }
}
