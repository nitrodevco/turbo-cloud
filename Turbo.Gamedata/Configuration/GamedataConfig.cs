namespace Turbo.Gamedata.Configuration;

/// <summary>
/// The hotel's gamedata (<c>Turbo:Gamedata</c>): where Habbo's updates come from, and the small
/// web host that serves the files the client loads. The files are built and Habbo is checked
/// whether or not the host is on; the host is off unless enabled, as every port into the hotel is.
/// </summary>
public sealed class GamedataConfig
{
    public const string SECTION_NAME = "Turbo:Gamedata";

    /// <summary>Whether the gamedata host listens.</summary>
    public bool Enabled { get; init; }

    /// <summary>Where the gamedata host listens. Loopback by default, for a reverse proxy in front.</summary>
    public string Url { get; init; } = "http://127.0.0.1:8094";

    /// <summary>
    /// Where clients reach the gamedata host (<c>https://gamedata.example.com</c>), for the
    /// addresses <c>/gamedata/hashes</c> lists. Empty takes the address a request came to.
    /// </summary>
    public string PublicUrl { get; init; } = "";

    /// <summary>The Habbo hotel updates are taken from: <c>com</c>, <c>nl</c>, <c>com.br</c>, ...</summary>
    public string HabboDomain { get; init; } = "com";

    /// <summary>How often Habbo is asked for a new release, in minutes. Zero checks only when staff ask.</summary>
    public int ReleaseCheckMinutes { get; init; } = 30;

    /// <summary>How long a request to Habbo may take, in seconds. Its furniture data is a large file.</summary>
    public int HabboTimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Which of Habbo's environments' HTML client the hotel's client libraries are built from:
    /// <c>production</c>, <c>sandbox</c>, or <c>auto</c> - the first of those with a release
    /// downloaded. The sandbox gets releases first.
    /// </summary>
    public string ClientEnvironment { get; init; } = "auto";

    /// <summary>Releases of Habbo's HTML client kept, newest first, besides any an environment is on.</summary>
    public int ClientKeepReleases { get; init; } = 5;

    /// <summary>
    /// What a release of Habbo's HTML client with a new chat library does to the chat styles:
    /// <c>follow</c> takes its styles and updates each the hotel has not changed since it took it,
    /// <c>replace</c> replaces the hotel's changes too, <c>off</c> takes nothing (the chat styles tab
    /// still can).
    /// </summary>
    public string ClientChatStyles { get; init; } = "follow";

    /// <summary>
    /// A release of Habbo's HTML client the hotel follows is built from as soon as it is downloaded,
    /// once no other asset job runs - what changed in it. Off, it is built at the next asset sync, or
    /// with Build now.
    /// </summary>
    public bool ClientBuildAfterDownload { get; init; } = true;

    /// <summary>How long downloading a release of Habbo's HTML client may take, in minutes (it is 20-150 MB).</summary>
    public int ClientDownloadMinutes { get; init; } = 30;

    /// <summary>
    /// Where a furniture's asset file is, read when an update is taken in for its states and the
    /// rest the file says: <c>{domain}</c>, <c>{revision}</c> and <c>{name}</c> (the classname
    /// without its <c>*N</c> colour) are filled in.
    /// </summary>
    public string FurnitureFileUrl { get; init; } =
        "https://images.habbo.{domain}/dcr/hof_furni/{revision}/{name}.swf";

    /// <summary>Furniture files downloaded at once while an update is taken in.</summary>
    public int FurnitureFileConcurrency { get; init; } = 8;

    /// <summary>Builds of each file kept besides the current one, for clients still holding their address.</summary>
    public int KeepBuilds { get; init; } = 10;

    /// <summary>Items an import preview lists; the counts cover every item.</summary>
    public int PreviewItemLimit { get; init; } = 500;

    /// <summary>Texts per page of a search.</summary>
    public int TextPageSize { get; init; } = 50;

    /// <summary>Furniture definitions per page of the panel's list.</summary>
    public int FurniturePageSize { get; init; } = 60;

    /// <summary>Change sets per page of the history.</summary>
    public int HistoryPageSize { get; init; } = 25;

    /// <summary>How long a text read from the database is kept before it is read again, in seconds.</summary>
    public int TextCacheSeconds { get; init; } = 300;

    /// <summary>Texts kept read at most; past it, what was kept is forgotten.</summary>
    public int TextCacheSize { get; init; } = 5000;

    /// <summary>Steps a text that is only another's key (<c>${other.key}</c>) is followed through.</summary>
    public int TextKeyDepth { get; init; } = 8;

    /// <summary>How long the figure data figures are checked against is kept before it is read again, in seconds.</summary>
    public int FigureCacheSeconds { get; init; } = 300;

    /// <summary>
    /// How long a client may use <c>/gamedata/&lt;file&gt;/0</c> before asking again, in seconds.
    /// A hashed address never changes, so it is cached for good.
    /// </summary>
    public int CurrentMaxAgeSeconds { get; init; } = 60;
}
