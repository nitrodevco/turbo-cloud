namespace Turbo.Database.Configuration;

public class DatabaseConfig
{
    public const string SECTION_NAME = "Turbo:Database";

    public string ConnectionString { get; init; } = string.Empty;
    public bool LoggingEnabled { get; init; } = false;
}
