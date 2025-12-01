using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Handshake;

public sealed record IsFirstLoginOfDayMessage : IComposer
{
    public required bool IsFirstLoginOfDay { get; init; }
}
