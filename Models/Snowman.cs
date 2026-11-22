namespace SnowDesk.Models;

public enum SnowmanHatType { TopHat, SantaHat, Beanie }
public enum SnowmanPropType { Sticks, Broom, CandyCane, CocoaMug, Mittens }

public class Snowman
{
    public float X { get; set; }
    public float BaseY { get; set; }
    public float Scale { get; set; } = 1.0f;
    public float WobblePhase { get; set; }
    public SnowmanHatType Hat { get; set; } = SnowmanHatType.TopHat;
    public SnowmanPropType Prop { get; set; } = SnowmanPropType.Sticks;
}