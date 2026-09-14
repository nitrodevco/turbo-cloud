namespace Turbo.Main.Configuration;

public class OrleansConfig
{
    public const string SECTION_NAME = "Turbo:Orleans";

    public string SiloAddress { get; init; } = "127.0.0.1";
    public int SiloPort { get; init; } = 11111;
    public int GatewayPort { get; init; } = 3000;
    public int GrainCollectionAgeMinutes { get; init; } = 2;
    public int RoomStreamPollMs { get; init; } = 10;
}
