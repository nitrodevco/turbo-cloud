namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>Where an asset job is.</summary>
public enum AssetJobStatus
{
    Running = 0,

    Done = 1,

    /// <summary>Stopped by an error; what it finished before is kept.</summary>
    Failed = 2,

    /// <summary>Stopped by staff, or by the server stopping; what it finished before is kept.</summary>
    Canceled = 3,
}
