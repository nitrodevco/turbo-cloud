using System.Threading;
using System.Threading.Tasks;

namespace Turbo.PacketHandlers.Camera;

/// <summary>Draws a stored render into its PNG; <see cref="CameraRenderer"/> is the one the server runs.</summary>
public interface ICameraRenderer
{
    /// <inheritdoc cref="CameraRenderer.RenderAsync"/>
    public Task<bool> RenderAsync(
        string jsonPath,
        string pngPath,
        CancellationToken ct,
        bool withSmall = false,
        string? frame = null,
        string? scenePath = null
    );
}
