namespace Turbo.Contracts.Plugins.Web;

/// <summary>
/// A plugin web route's answer: a status with a JSON value, or an error message
/// (<c>{"error": "..."}</c>) the caller shows.
/// </summary>
public sealed record PluginWebResult(int Status, object? Value = null, string? Error = null)
{
    public static PluginWebResult Ok(object? value) => new(200, value);

    public static PluginWebResult NoContent() => new(204);

    public static PluginWebResult BadRequest(string error) => new(400, Error: error);

    public static PluginWebResult Forbidden(string error) => new(403, Error: error);

    public static PluginWebResult NotFound(string error) => new(404, Error: error);
}
