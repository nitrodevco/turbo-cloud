using Turbo.Contracts.Abstractions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Core.Envelope;

namespace Turbo.Messaging;

public record MessageEnvelope : EnvelopeBase<IMessageEvent>
{
    public required ISessionContext Session { get; init; }
}
