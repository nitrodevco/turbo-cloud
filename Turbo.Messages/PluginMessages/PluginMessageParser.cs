using System;
using System.Reflection;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Plugins.Messages;

namespace Turbo.Messages.PluginMessages;

/// <summary>Reads one plugin message's header with its schema, into its <see cref="PluginMessageEvent{T}"/>.</summary>
internal sealed class PluginMessageParser : IParser
{
    private static readonly MethodInfo EVENT_FACTORY = typeof(PluginMessageParser).GetMethod(
        nameof(EventFactory),
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private readonly PluginMessageSchema _schema;
    private readonly Func<IPluginMessage, IMessageEvent> _wrap;

    public PluginMessageParser(PluginMessageSchema schema)
    {
        _schema = schema;
        _wrap =
            (Func<IPluginMessage, IMessageEvent>)
                EVENT_FACTORY.MakeGenericMethod(schema.Type).Invoke(null, null)!;
    }

    public IMessageEvent Parse(IClientPacket packet) => _wrap(_schema.Read(packet));

    private static Func<IPluginMessage, IMessageEvent> EventFactory<T>()
        where T : IPluginMessage => message => new PluginMessageEvent<T>((T)message);
}
