using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;

namespace Turbo.Plugins;

/// <summary>
/// System.Text.Json keeps what it learns of every type it writes (its accessors, in a static cache it
/// trims only as it is used again), so a plugin whose own types were written as JSON - a web route's or
/// an admin endpoint's answer - stays loaded after it unloads. The serializer clears that cache for hot
/// reload through its <see cref="MetadataUpdateHandlerAttribute"/>; an unload asks it the same.
/// </summary>
internal static class SerializerCaches
{
    private static readonly Action<Type[]?>? CLEAR = Find();

    public static void Clear()
    {
        try
        {
            CLEAR?.Invoke(null);
        }
        catch (Exception)
        {
            // Only memory is at stake; the unload's own wait says if the plugin stayed.
        }
    }

    private static Action<Type[]?>? Find()
    {
        var method = typeof(JsonSerializer)
            .Assembly.GetCustomAttributes<MetadataUpdateHandlerAttribute>()
            .Select(x =>
                x.HandlerType.GetMethod(
                    "ClearCache",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    [typeof(Type[])]
                )
            )
            .FirstOrDefault(x => x is not null);

        return method?.CreateDelegate<Action<Type[]?>>();
    }
}
