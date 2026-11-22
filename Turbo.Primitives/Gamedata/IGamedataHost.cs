namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The gamedata host as the rest of the server needs it: where clients reach it, for code outside
/// Turbo.Gamedata that writes one of its addresses into what the client is sent.
/// </summary>
public interface IGamedataHost
{
    /// <summary>
    /// The hotel's public address for the gamedata host, without a trailing slash; null when the
    /// hotel set none (the host then answers with whatever address a request came to).
    /// </summary>
    public string? PublicUrl { get; }
}
