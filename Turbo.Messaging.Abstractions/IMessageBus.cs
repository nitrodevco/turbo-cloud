using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Abstractions.Envelope;

namespace Turbo.Messaging.Abstractions;

public interface IMessageBus : IEnvelopeBus<IMessageEvent> { }
