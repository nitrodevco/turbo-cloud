using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Turbo.Primitives.Plugins.Messages.Snapshots;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// The plugin messages the hotel has now, as the host sees them: the names, the headers it gave
/// them and their schemas. Plugins do not use it; their messages are found in their assemblies and
/// registered as they load.
/// </summary>
public interface IPluginMessageDirectory
{
    /// <summary>Every message now, by name; what a client that accepted <c>client.messages</c> is sent.</summary>
    ImmutableArray<PluginMessageSnapshot> Messages { get; }

    /// <summary>Raised after messages are added or taken away.</summary>
    event System.Action? Changed;

    /// <summary>The header of the message named <paramref name="name"/>, while it is registered.</summary>
    bool TryGetHeader(string name, out int header);

    /// <summary>
    /// Registers the messages of the plugin <paramref name="owner"/>, each given a header from the
    /// plugin space that no packet uses; disposing the handle takes them away. A name that does not
    /// start with <c>owner.</c>, a type without <see cref="PluginMessageAttribute"/>, a field of a
    /// type the codec cannot carry, or a name already registered refuses all of them.
    /// </summary>
    /// <exception cref="ArgumentException">A message is not one the owner may declare.</exception>
    /// <exception cref="InvalidOperationException">A name is already registered.</exception>
    IDisposable Register(string owner, IReadOnlyCollection<Type> messageTypes);
}
