namespace SnowDesk.Models;

public class SantaSleigh
{
    public float X { get; set; }
    public float Y { get; set; }
    public float SpeedX { get; set; }
    public float WavePhase { get; set; }
    public bool IsActive { get; set; }
    public List<(float X, float Y, float Alpha, float Size)> Trail { get; } = new();
}