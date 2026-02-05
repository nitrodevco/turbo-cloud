using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record GetExtendedProfileMessage : IMessageEvent 
{
    public PlayerId UserId { get; init; }
}
