namespace Turbo.Primitives.Movements;

/// <summary>
/// The causes core writes itself. A marker row moves nothing (asset id 0, amount 0, both places
/// none): it tells a consumer that recording started or stopped, so "nothing happened" and
/// "nothing was recorded" read differently. Its cause ref says why.
/// </summary>
public static class MovementCauses
{
    public const string RECORDING_STARTED = "recording.started";
    public const string RECORDING_STOPPED = "recording.stopped";

    /// <summary>A marker's cause ref: the server started or stopped while recording was on.</summary>
    public const string MARKER_REF_SERVER = "server";

    /// <summary>A marker's cause ref: <c>Turbo:Movements:Enabled</c> was changed while the server ran.</summary>
    public const string MARKER_REF_SETTING = "setting";
}
