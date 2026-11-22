namespace SnowDesk.Models;

public class Snowball
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Radius { get; set; } = 4.5f;
    public bool IsHeld { get; set; }
    public bool IsThrown { get; set; }
}