namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// A message between a plugin's server half and its client half, declared once as a
/// <c>sealed record</c> with <see cref="PluginMessageAttribute"/>. Its fields are the record's
/// primary constructor parameters, in order; the host assigns its header and tells clients the name,
/// header and field schema (<c>client.messages</c>, <c>docs/plugin-messages.md</c>). Any message
/// can go either way.
/// </summary>
public interface IPluginMessage;
