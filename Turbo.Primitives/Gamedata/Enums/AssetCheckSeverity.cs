namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>How much a check of the bundles matters.</summary>
public enum AssetCheckSeverity
{
    /// <summary>The client draws something wrong: a placeholder, a file that is not there.</summary>
    Error = 0,

    /// <summary>Worth a look: a library that failed, an effect or pet with nothing to draw it, a bundle nothing uses.</summary>
    Warning = 1,
}
