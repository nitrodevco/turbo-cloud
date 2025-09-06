using Turbo.Pipeline.Abstractions.Envelope;
using Turbo.Primitives;

namespace Turbo.Events.Abstractions;

public interface IEventBus : IEnvelopeBus<IEvent> { }
