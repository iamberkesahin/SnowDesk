namespace SnowDesk.Models;

public class SantaSleigh
{
    public bool IsActive { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float SpeedX { get; set; } = 165f;
    public int Direction { get; set; } = 1; // 1: Soldan Sağa, -1: Sağdan Sola
    public float WavePhase { get; set; }
    public List<(float X, float Y, float Alpha, float Size)> Trail { get; } = new();
}
