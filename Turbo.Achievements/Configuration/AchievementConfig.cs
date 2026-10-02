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

    /// <summary>Hotel-provided PNG badge asset directory, checked on catalog import.</summary>
    public string BadgeAssetDirectory { get; set; } = "";
    public int MaxDefinitions { get; set; } = 1000;
    public int MaxDistinctValues { get; set; } = 100000;
    public string DefaultCategory { get; set; } = "identity";
    public bool ShowCongratulationsDialog { get; set; } = true;
}
