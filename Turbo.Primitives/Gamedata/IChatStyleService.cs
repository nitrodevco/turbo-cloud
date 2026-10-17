using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The hotel's chat bubble styles whole: who may speak with each (what the server checks every
/// line against) and how the client draws it - its reg points, flags and bitmaps, built into the
/// <c>habbo-free-flow-chat-com</c> template bundle after every change and published with the
/// other bundles. A change reaches the next line said; the look reaches clients once published.
/// </summary>
public interface IChatStyleService
{
    Task<ChatStyleList> ListAsync(CancellationToken ct);

    /// <summary>One of a style's bitmaps as PNG; null when it has none of that file.</summary>
    Task<byte[]?> GetBitmapAsync(int id, string file, CancellationToken ct);

    /// <summary>
    /// Adds a style (id 0) or changes one; its row id. Throws <see cref="System.ArgumentException"/>,
    /// with why, for anything it can't keep: an asset id or client id another style has, a name the
    /// client's asset ids can't be, reg points that aren't the client's, a bitmap that isn't an
    /// image, or the normal bubble made anything but everyone's.
    /// </summary>
    Task<int> SaveAsync(int id, ChatStyleEdit edit, PlayerId player, CancellationToken ct);

    /// <summary>
    /// Removes a style; false when there is none. Throws <see cref="System.ArgumentException"/> for
    /// the normal bubble and a style players own.
    /// </summary>
    Task<bool> DeleteAsync(int id, PlayerId player, CancellationToken ct);

    /// <summary>
    /// Takes in a habbo-free-flow-chat-com bundle (as nitro-studio or a published hotel has it):
    /// each style it defines, by client id, with its look and bitmaps, and the library's other
    /// bitmaps. Who may speak with a style the hotel has already is kept as the hotel set it.
    /// Throws <see cref="System.ArgumentException"/> for a file that is no such bundle.
    /// </summary>
    Task<ChatStyleImportResult> ImportBundleAsync(
        byte[] bundle,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// As <see cref="ImportBundleAsync"/>, from Habbo's HTML client (the release the hotel builds
    /// from, <see cref="IHabboClientService"/>): its chat library's styles and other bitmaps. A
    /// style the hotel changed since it took it from the client keeps the hotel's look unless
    /// <paramref name="replaceChanged"/>. Throws <see cref="System.InvalidOperationException"/>
    /// when no release is downloaded.
    /// </summary>
    Task<ChatStyleImportResult> ImportFromClientAsync(
        PlayerId player,
        bool replaceChanged,
        CancellationToken ct
    );

    /// <summary>
    /// The client's chat styles taken again when the release followed has a chat library the hotel
    /// has not taken yet, as <c>Turbo:Gamedata:ClientChatStyles</c> says (<c>follow</c> keeps the
    /// hotel's changes, <c>replace</c> replaces them, <c>off</c> takes nothing); null when nothing
    /// was taken.
    /// </summary>
    Task<ChatStyleImportResult?> FollowClientAsync(PlayerId player, CancellationToken ct);
}
