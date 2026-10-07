namespace Turbo.Assets.Swf;

/// <summary>
/// An image tag of an SWF as stored, after its character id: a DefineBitsLossless(2) or
/// DefineBitsJPEG2/3 body, which the converter decodes.
/// </summary>
public sealed record SwfImageTag(int CharacterId, int Code, byte[] Data);
