using System.Collections.Generic;

namespace Turbo.Admin.Configuration;

public class AdminConfig
{
    public const string SECTION_NAME = "Turbo:Admin";

    public required int Port { get; init; } = 5250;
    public required List<string> AllowedDevOrigins { get; init; } = [];
    public required AdminJwtConfig Jwt { get; init; }
    public required int DefaultPageSize { get; init; } = 50;
    public required int MaxPageSize { get; init; } = 200;
    public required int OrleansGatewayPort { get; init; } = 3000;
    public required EmulatorProcessConfig Emulator { get; init; }
}

public class AdminJwtConfig
{
    public required string Secret { get; init; }
    public required string Issuer { get; init; }
    public required int ExpiryMinutes { get; init; } = 480;
}

public class EmulatorProcessConfig
{
    public required string ExecutablePath { get; init; }
    public required string Arguments { get; init; } = string.Empty;
    public required string WorkingDirectory { get; init; }
    public required string GracefulShutdownCommand { get; init; } = "quit";
    public required int GracefulShutdownTimeoutSeconds { get; init; } = 15;
}
