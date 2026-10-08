using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Sound;

public record GetOfficialSongIdMessage : IMessageEvent
{
    /// <summary>The catalog code of an official song (a product's extra parameter that is not a number).</summary>
    public required string Code { get; init; }
}
