using Turbo.Pipeline.Abstractions.Envelope;
using Turbo.Primitives;

namespace Turbo.Messaging.Abstractions;

public interface IMessageBus : IEnvelopeBus<IMessageEvent> { }
