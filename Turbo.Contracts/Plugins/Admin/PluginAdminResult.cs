namespace Turbo.Contracts.Plugins.Admin;

/// <summary>
/// A plugin admin endpoint's answer: a status with a JSON value, a file, or an error message the
/// panel shows.
/// </summary>
public sealed record PluginAdminResult(
    int Status,
    object? Value = null,
    string? Error = null,
    byte[]? File = null,
    string? ContentType = null,
    string? FileName = null
)
{
    public static PluginAdminResult Ok(object? value) => new(200, value);

    public static PluginAdminResult NoContent() => new(204);

    public static PluginAdminResult NotFound(string error) => new(404, Error: error);

    public static PluginAdminResult BadRequest(string error) => new(400, Error: error);

    public static PluginAdminResult FileOf(byte[] file, string contentType, string fileName) =>
        new(200, File: file, ContentType: contentType, FileName: fileName);
}
