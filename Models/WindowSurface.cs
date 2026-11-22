using SnowDesk.Native;

namespace SnowDesk.Models;

public class WindowSurface
{
    public IntPtr Handle { get; }
    public Win32Interop.RECT Bounds { get; set; }
    public float[] SnowHeights { get; private set; }
    public float[] OrganicMultiplier { get; private set; }
    public List<float> TreeOffsetsX { get; } = new();
    public float NoiseSeed { get; }

    public const int BucketSize = 4;
    public const float MaxSnowDepth = 34f;

    public WindowSurface(IntPtr handle, Win32Interop.RECT bounds)
    {
        Handle = handle;
        Bounds = bounds;
        NoiseSeed = (float)(Random.Shared.NextDouble() * 100.0);
        int buckets = Math.Max(1, bounds.Width / BucketSize);
        SnowHeights = new float[buckets];
        OrganicMultiplier = new float[buckets];
        RebuildOrganicProfile();
    }

    private void RebuildOrganicProfile()
    {
        int len = OrganicMultiplier.Length;
        for (int i = 0; i < len; i++)
        {
            float norm = (float)i / Math.Max(1, len - 1);
            // Kenarlara doğru incelen, ortalarda organik tepecikler oluşturan profil
            float edgeTaper = MathF.Sin(norm * MathF.PI);
            float wave1 = MathF.Sin(i * 0.14f + NoiseSeed) * 0.45f;
            float wave2 = MathF.Cos(i * 0.37f + NoiseSeed * 1.7f) * 0.25f;
            OrganicMultiplier[i] = Math.Clamp((0.55f + wave1 + wave2) * (0.35f + 0.65f * edgeTaper), 0.15f, 1.55f);
        }
    }

    public void UpdateBounds(Win32Interop.RECT newBounds, out float movementDelta)
    {
        float dx = Math.Abs(newBounds.Left - Bounds.Left);
        float dy = Math.Abs(newBounds.Top - Bounds.Top);
        movementDelta = dx + dy;

        if (newBounds.Width != Bounds.Width)
        {
            int newBuckets = Math.Max(1, newBounds.Width / BucketSize);
            var resized = new float[newBuckets];
            Array.Copy(SnowHeights, resized, Math.Min(SnowHeights.Length, newBuckets));
            SnowHeights = resized;
            OrganicMultiplier = new float[newBuckets];
            RebuildOrganicProfile();
        }

        Bounds = newBounds;
    }

    public void SmoothSnow()
    {
        for (int i = 1; i < SnowHeights.Length - 1; i++)
        {
            SnowHeights[i] = (SnowHeights[i - 1] + SnowHeights[i] * 2.2f + SnowHeights[i + 1]) / 4.2f;
        }
    }

    public void ShedSnow(float ratio)
    {
        for (int i = 0; i < SnowHeights.Length; i++)
        {
            SnowHeights[i] = Math.Max(0, SnowHeights[i] - ratio);
        }
    }
}