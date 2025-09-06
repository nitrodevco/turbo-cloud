using Turbo.Pipeline.Core.Envelope;
using Turbo.Primitives;

namespace Turbo.Events;

public record EventEnvelope : EnvelopeBase<IEvent> { }
