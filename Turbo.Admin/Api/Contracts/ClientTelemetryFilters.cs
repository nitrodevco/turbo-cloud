namespace Turbo.Admin.Api.Contracts;

/// <summary>The browsers, systems and kinds of device reported over the range, to filter by.</summary>
public sealed record ClientTelemetryFilters(string[] Browsers, string[] Oses, string[] Devices);
