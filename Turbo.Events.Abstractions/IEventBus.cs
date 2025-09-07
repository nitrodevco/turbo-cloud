using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Abstractions.Envelope;

namespace Turbo.Events.Abstractions;

public interface IEventBus : IEnvelopeBus<IEvent> { }
