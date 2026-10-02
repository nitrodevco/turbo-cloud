using System;

namespace Turbo.Main.Configuration;

public sealed class TelemetryConfig
{
    public const string SECTION_NAME = "Turbo:Telemetry";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = "http://localhost:4317";

    public string ServiceName { get; set; } = "turbo-cloud";

    public double TraceSampleRatio { get; set; } = 1.0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException($"{SECTION_NAME}:ServiceName cannot be empty.");
        }

        if (
            !Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)
        )
        {
            throw new InvalidOperationException(
                $"{SECTION_NAME}:Endpoint must be an absolute HTTP or HTTPS URI."
            );
        }

        if (double.IsNaN(TraceSampleRatio) || TraceSampleRatio is < 0 or > 1)
        {
            throw new InvalidOperationException(
                $"{SECTION_NAME}:TraceSampleRatio must be between 0 and 1."
            );
        }
    }
}
