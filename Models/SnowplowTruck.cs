namespace SnowDesk.Models;

public class SnowplowTruck
{
    public bool IsActive { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float SpeedX { get; set; } = 240f;
    public float WheelAngle { get; set; }
}