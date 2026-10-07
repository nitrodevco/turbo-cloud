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
