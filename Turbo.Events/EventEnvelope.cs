using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Core.Envelope;

namespace Turbo.Events;

public record EventEnvelope : EnvelopeBase<IEvent> { }
