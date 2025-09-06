using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Core.Envelope;
using Turbo.Primitives;

namespace Turbo.Messaging;

public record MessageEnvelope : EnvelopeBase<IMessageEvent>
{
    public required ISessionContext Session { get; init; }
}
