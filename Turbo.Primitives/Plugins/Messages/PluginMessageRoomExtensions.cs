using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// Sending a plugin message to a room: the room's own <c>SendComposerToRoomAsync</c> with the
/// message written by its schema (<see cref="PluginMessageComposer"/>). From room code that holds
/// another send (a room object's, a wired context's), pass <c>PluginMessageComposer.Compose(message)</c>
/// to it the same way.
/// </summary>
public static class PluginMessageRoomExtensions
{
    /// <summary>Sends <paramref name="message"/> to everyone in the room who accepted <c>client.messages</c>.</summary>
    public static Task SendPluginMessageToRoomAsync(
        this IRoomGrain room,
        IPluginMessage message,
        CancellationToken ct
    ) => room.SendComposerToRoomAsync(PluginMessageComposer.Compose(message), ct);
}
