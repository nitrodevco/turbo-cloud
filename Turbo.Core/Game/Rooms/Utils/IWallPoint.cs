namespace Turbo.Core.Game.Rooms.Utils;

public interface IWallPoint
{
    public int X { get; set; }

    public int Y { get; set; }

    public double Z { get; set; }

    public Rotation Rotation { get; set; }

    public int WallOffset { get; set; }

    public void SetRotation(Rotation? rotation);

    public IWallPoint Clone();

    public string ToString();
}
