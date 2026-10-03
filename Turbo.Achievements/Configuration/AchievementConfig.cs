namespace Turbo.Achievements.Configuration;

public sealed class AchievementConfig
{
    public const string SECTION_NAME = "Turbo:Achievements";

    /// <summary>Durable recovery polling interval, in seconds.</summary>
    public int RecoverySeconds { get; set; } = 5;

    /// <summary>Maximum players admitted to one recovery pass.</summary>
    public int RecoveryBatchSize { get; set; } = 100;

    /// <summary>Maximum facts consumed in one player's turn.</summary>
    public int FactBatchSize { get; set; } = 100;

    /// <summary>Days a processed fact is kept for idempotent admission; zero keeps them forever.</summary>
    public int FactRetentionDays { get; set; } = 30;

    /// <summary>
    /// Installs the shipped Habbo achievement catalog when the hotel has none. A hotel that
    /// already has definitions is never touched, so its edits survive.
    /// </summary>
    public bool InstallDefaults { get; set; } = true;

    /// <summary>Hotel-provided PNG badge asset directory, checked on catalog import.</summary>
    public string BadgeAssetDirectory { get; set; } = "";

    /// <summary>
    /// The URL the client loads badge images from, with <c>%badgename%</c> where the badge code
    /// goes (the client's <c>badge.asset.url</c>, for example
    /// <c>https://images.example.com/badges/%badgename%.gif</c>). When set, catalog imports check
    /// badge images here instead of in <see cref="BadgeAssetDirectory"/>.
    /// </summary>
    public string BadgeAssetUrl { get; set; } = "";
    public int MaxDefinitions { get; set; } = 1000;
    public int MaxDistinctValues { get; set; } = 100000;

    /// <summary>Most values one definition's match list may hold.</summary>
    public int MaxMatchValues { get; set; } = 1000;
    public string DefaultCategory { get; set; } = "identity";
    public bool ShowCongratulationsDialog { get; set; } = true;

    /// <summary>
    /// Lists every archived achievement to every player. By default a player only sees the archived
    /// achievements they have progressed, so a new account is not shown things it can never earn.
    /// </summary>
    public bool ArchiveShowsAll { get; set; }
}
