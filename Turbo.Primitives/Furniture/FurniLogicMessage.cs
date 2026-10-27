using System.Text.Json;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// A message from a plugin to its own furni logic in a room (<c>IRoomGrain.InvokeFurniLogicAsync</c>),
/// typically from one of its packet handlers. <see cref="Key"/> is the logic key the item must run
/// (the plugin's <c>[FurniLogic]</c> key); <see cref="Payload"/> is whatever the plugin serialised.
/// Strings only: a plugin's own types never cross grains, since its assembly can be reloaded.
/// <see cref="Sender"/> is the player it is on behalf of, when there is one.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurniLogicMessage(
    [property: Id(0)] string Key,
    [property: Id(1)] string Payload,
    [property: Id(2)] PlayerId? Sender = null
)
{
    /// <summary>A message whose payload is <paramref name="body"/> as JSON.</summary>
    public static FurniLogicMessage Of<T>(string key, T body, PlayerId? sender = null) =>
        new(key, JsonSerializer.Serialize(body), sender);

    /// <summary>The payload read back as JSON; default when it is empty or not a <typeparamref name="T"/>.</summary>
    public T? Read<T>()
    {
        if (string.IsNullOrEmpty(Payload))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(Payload);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

/// <summary>What became of a <see cref="FurniLogicMessage"/>.</summary>
public enum FurniLogicReplyStatus
{
    /// <summary>The logic took it; <see cref="FurniLogicReply.Payload"/> is its answer, if it gave one.</summary>
    Handled,

    /// <summary>The logic was asked and did not take it (its default).</summary>
    NotHandled,

    /// <summary>No floor item of that id stands in the room.</summary>
    UnknownItem,

    /// <summary>The item stands there but does not run the logic the message names.</summary>
    WrongLogic,

    /// <summary>The logic threw; the failure is logged by the room.</summary>
    Failed,
}

/// <summary>A furni logic's answer to a <see cref="FurniLogicMessage"/>, or why there was none.</summary>
[GenerateSerializer, Immutable]
public sealed record FurniLogicReply(
    [property: Id(0)] FurniLogicReplyStatus Status,
    [property: Id(1)] string? Payload = null
)
{
    public static readonly FurniLogicReply NotHandled = new(FurniLogicReplyStatus.NotHandled);

    public static readonly FurniLogicReply UnknownItem = new(FurniLogicReplyStatus.UnknownItem);

    public static readonly FurniLogicReply WrongLogic = new(FurniLogicReplyStatus.WrongLogic);

    public static readonly FurniLogicReply Failed = new(FurniLogicReplyStatus.Failed);

    /// <summary>Taken, with <paramref name="payload"/> as the answer when there is one.</summary>
    public static FurniLogicReply Handled(string? payload = null) =>
        new(FurniLogicReplyStatus.Handled, payload);

    /// <summary>Taken, with <paramref name="body"/> as JSON for the answer.</summary>
    public static FurniLogicReply Handled<T>(T body) =>
        new(FurniLogicReplyStatus.Handled, JsonSerializer.Serialize(body));

    public bool IsHandled => Status == FurniLogicReplyStatus.Handled;

    /// <summary>The answer read back as JSON; default when there is none or it is not a <typeparamref name="T"/>.</summary>
    public T? Read<T>()
    {
        if (string.IsNullOrEmpty(Payload))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(Payload);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
