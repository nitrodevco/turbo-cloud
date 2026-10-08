using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record ApproveNameMessage : IMessageEvent
{
    public required string Name { get; init; }
    public required ApproveNameType Type { get; init; }
}
