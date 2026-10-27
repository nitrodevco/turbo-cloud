using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Bots;

/// <summary>
/// Something a placed bot is told to do by the hotel rather than by a player's skill menu: what
/// the hotel API's bots (<c>IHotelBots</c>) send the room. The room carries it out as it carries out
/// a wired bot action, with the same flood limit and word filter.
/// </summary>
[GenerateSerializer, Immutable]
public abstract record BotOrder;

/// <summary>The bot says (or shouts) a line to the room.</summary>
[GenerateSerializer, Immutable]
public sealed record BotSayOrder([property: Id(0)] string Text, [property: Id(1)] bool Shout)
    : BotOrder;

/// <summary>The bot whispers a line that only <see cref="Player"/> sees.</summary>
[GenerateSerializer, Immutable]
public sealed record BotWhisperOrder(
    [property: Id(0)] PlayerId Player,
    [property: Id(1)] string Text
) : BotOrder;

/// <summary>The bot walks to a tile.</summary>
[GenerateSerializer, Immutable]
public sealed record BotWalkOrder([property: Id(0)] int X, [property: Id(1)] int Y) : BotOrder;

/// <summary>The bot walks onto a floor furni of the room, by its item id.</summary>
[GenerateSerializer, Immutable]
public sealed record BotWalkToItemOrder([property: Id(0)] int ItemId) : BotOrder;

/// <summary>The bot waves, blows a kiss, laughs, cries or jumps.</summary>
[GenerateSerializer, Immutable]
public sealed record BotGestureOrder([property: Id(0)] AvatarExpressionType Gesture) : BotOrder;

/// <summary>The bot dances (or stops, with <see cref="AvatarDanceType.None"/>).</summary>
[GenerateSerializer, Immutable]
public sealed record BotDanceOrder([property: Id(0)] AvatarDanceType Dance) : BotOrder;

/// <summary>The bot holds up a sign, 0 to 17 as a player's <c>:sign</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record BotSignOrder([property: Id(0)] int Sign) : BotOrder;
