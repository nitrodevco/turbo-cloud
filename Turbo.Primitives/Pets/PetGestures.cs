namespace Turbo.Primitives.Pets;

/// <summary>
/// Gesture tokens sent as <c>gst &lt;id&gt;</c> in a pet's status string; the client shows one
/// for three seconds and resolves it against the pet's visualization.
/// </summary>
public static class PetGestures
{
    public const string SMILE = "sml";
    public const string SAD = "sad";
    public const string SPEAK = "spk";
    public const string CROAK = "crk";
    public const string RELAX = "rlx";
    public const string WINGS = "wng";
    public const string FLAME = "flm";
    public const string SURPRISED = "srp";
}
