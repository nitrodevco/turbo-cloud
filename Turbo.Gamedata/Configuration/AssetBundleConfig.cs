using Turbo.Primitives.Settings;

namespace Turbo.Gamedata.Configuration;

/// <summary>
/// The hotel's asset bundles (<c>Turbo:Assets</c>): the <c>.nitro</c> files the client loads for
/// furniture, avatar effects and pets, converted from Habbo's files and kept on this server, and
/// how they are taken from Habbo and published to where the client loads them.
/// </summary>
public sealed class AssetBundleConfig
{
    public const string SECTION_NAME = "Turbo:Assets";

    /// <summary>
    /// The folder the bundles are kept in, laid out as an asset host serves them
    /// (<c>bundled/furniture</c>, <c>bundled/effects</c>, <c>bundled/pet</c>). A relative path is
    /// under the server's own folder. Every server converting into it must share it.
    /// </summary>
    public string Directory { get; init; } = "assets";

    /// <summary>
    /// Where one of Habbo's client libraries is (an effect's, a pet's, the effect map):
    /// <c>{domain}</c>, <c>{revision}</c> (the client's, from <c>flash.client.url</c>) and
    /// <c>{name}</c> are filled in. Furniture files come from <c>Turbo:Gamedata:FurnitureFileUrl</c>.
    /// </summary>
    public string GordonFileUrl { get; init; } =
        "https://images.habbo.{domain}/gordon/flash-assets-{revision}/{name}";

    /// <summary>
    /// Whether a Habbo check that finds a release (the timer or "Check now") goes on to download
    /// and convert every bundle that is missing or has a newer revision. Off, a sync runs only when
    /// staff start one.
    /// </summary>
    public bool SyncAfterCheck { get; init; } = true;

    /// <summary>Files downloaded from Habbo at once during a sync.</summary>
    public int DownloadConcurrency { get; init; } = 6;

    /// <summary>Files converted at once during a sync; converting is the heavy part.</summary>
    public int ConvertConcurrency { get; init; } = 4;

    /// <summary>Files sent at once while publishing.</summary>
    public int PublishConcurrency { get; init; } = 4;

    /// <summary>How long connecting to a publish target may take, in seconds.</summary>
    public int PublishTimeoutSeconds { get; init; } = 30;

    /// <summary>The largest file an upload in the panel may be, in megabytes.</summary>
    public int UploadMaxMegabytes { get; init; } = 64;

    /// <summary>Lines of a job's log kept for the panel; older ones are let go.</summary>
    public int JobLogLimit { get; init; } = 300;

    /// <summary>Bundles per page of the panel's list.</summary>
    public int PageSize { get; init; } = 60;

    /// <summary>Example items each check of the bundles lists; its count covers them all.</summary>
    public int CheckSampleLimit { get; init; } = 25;

    /// <summary>
    /// The key publish targets' passwords are sealed with: 32 bytes as base64. Empty, a key is made
    /// once and kept in the bundle folder (<c>.publish-key</c>), so a database dump alone does not
    /// give the passwords away. Changing it makes the saved passwords unreadable, to be entered again.
    /// </summary>
    [SecretSetting]
    public string SecretKey { get; init; } = "";
}
