using Turbo.Primitives.Settings;

namespace Turbo.Main.Configuration;

/// <summary>The silo and its streams: what the whole server runs on.</summary>
[StartupSetting]
public class OrleansConfig
{
    public const string SECTION_NAME = "Turbo:Orleans";

    public string SiloAddress { get; init; } = "127.0.0.1";
    public int SiloPort { get; init; } = 11111;
    public int GatewayPort { get; init; } = 3000;
    public int GrainCollectionAgeMinutes { get; init; } = 2;
    public int RoomStreamPollMs { get; init; } = 10;

    /// <summary>
    /// How long a delivered stream message stays in the memory stream cache before it may be
    /// purged. Orleans keeps them five minutes by default for consumers that rewind; room and
    /// player streams carry live traffic nobody rewinds, and at a busy hotel's message rate five
    /// minutes of it is hundreds of megabytes.
    /// </summary>
    public int StreamCacheMinSeconds { get; init; } = 30;

    /// <summary>The oldest a message may be before it is purged regardless (Orleans: thirty minutes).</summary>
    public int StreamCacheMaxSeconds { get; init; } = 60;
}
