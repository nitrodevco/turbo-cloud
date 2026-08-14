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
}

public class AdminJwtConfig
{
    public required string Secret { get; init; }
    public required string Issuer { get; init; }
    public required int ExpiryMinutes { get; init; } = 480;
}
