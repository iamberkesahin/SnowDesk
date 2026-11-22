using SnowDesk.Native;

namespace SnowDesk.Models;

public class WindowSurface
{
    public const int BucketSize = 4;
    public const float MaxSnowDepth = 26f;

    public IntPtr Hwnd { get; }
    public Win32Interop.RECT Bounds { get; private set; }
    public float[] SnowHeights { get; private set; }
    public float[] OrganicMultiplier { get; private set; }
    public float[] IcicleLengths { get; private set; }
    public List<float> TreeOffsetsX { get; } = new();

    private float _shakeEnergy;
    private int _lastDirX;

    public WindowSurface(IntPtr hwnd, Win32Interop.RECT bounds)
    {
        Hwnd = hwnd;
        Bounds = bounds;
        int buckets = Math.Max(1, bounds.Width / BucketSize);
        SnowHeights = new float[buckets];
        OrganicMultiplier = new float[buckets];
        IcicleLengths = new float[buckets];
        GenerateOrganicProfile(buckets);
    }

    private void GenerateOrganicProfile(int buckets)
    {
        var rng = new Random(Hwnd.GetHashCode());
        float p1 = (float)(rng.NextDouble() * Math.PI * 2);
        float p2 = (float)(rng.NextDouble() * Math.PI * 2);

        for (int i = 0; i < buckets; i++)
        {
            float edge = MathF.Sin((float)i / Math.Max(1, buckets - 1) * MathF.PI);
            float wave = 0.65f + 0.35f * MathF.Sin(i * 0.14f + p1) + 0.25f * MathF.Cos(i * 0.31f + p2);
            OrganicMultiplier[i] = Math.Clamp(edge * wave * 1.35f, 0.08f, 1.45f);
        }
    }

    public void UpdateBounds(Win32Interop.RECT newBounds, out float moveDelta, out bool isShaken)
    {
        int dx = newBounds.Left - Bounds.Left;
        int dy = newBounds.Top - Bounds.Top;
        moveDelta = MathF.Sqrt(dx * dx + dy * dy);

        // Sağ-sol hızlı yön değişimi (Pencere Sallama) tespiti
        int dirX = Math.Sign(dx);
        if (dirX != 0 && dirX != _lastDirX && Math.Abs(dx) > 6)
        {
            _shakeEnergy += 1.0f;
        }
        _lastDirX = dirX;
        _shakeEnergy = Math.Max(0f, _shakeEnergy - 0.08f);
        isShaken = _shakeEnergy >= 2.2f;
        if (isShaken) _shakeEnergy = 0f;

        if (newBounds.Width != Bounds.Width)
        {
            int newBuckets = Math.Max(1, newBounds.Width / BucketSize);
            var resizedSnow = new float[newBuckets];
            var resizedIcicles = new float[newBuckets];
            Array.Copy(SnowHeights, resizedSnow, Math.Min(SnowHeights.Length, newBuckets));
            Array.Copy(IcicleLengths, resizedIcicles, Math.Min(IcicleLengths.Length, newBuckets));
            SnowHeights = resizedSnow;
            IcicleLengths = resizedIcicles;
            OrganicMultiplier = new float[newBuckets];
            GenerateOrganicProfile(newBuckets);
        }

        Bounds = newBounds;
    }

    public void GrowIcicles(float dt)
    {
        for (int i = 2; i < IcicleLengths.Length - 2; i += 4)
        {
            if (SnowHeights[i] > 4f && IcicleLengths[i] < 24f * OrganicMultiplier[i])
            {
                IcicleLengths[i] += dt * 0.85f * OrganicMultiplier[i];
            }
        }
    }

    public void ShedSnow(float amount)
    {
        for (int i = 0; i < SnowHeights.Length; i++)
            SnowHeights[i] = Math.Max(0f, SnowHeights[i] - amount);
    }

    public void SmoothSnow()
    {
        for (int i = 1; i < SnowHeights.Length - 1; i++)
            SnowHeights[i] = (SnowHeights[i - 1] + SnowHeights[i] * 2f + SnowHeights[i + 1]) / 4f;
    }
}