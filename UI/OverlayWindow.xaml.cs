using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SnowDesk.Engine;
using SnowDesk.Models;
using SnowDesk.Native;

namespace SnowDesk.UI;

public partial class OverlayWindow : Window
{
    public SnowPhysicsEngine Engine { get; private set; }
    private readonly AppSettings _settings;
    private readonly Stopwatch _stopwatch = new();
    private double _lastFrameTime;
    private double _lastWindowScanTime;
    private IntPtr _hwnd;
    private bool _isInitializedSize;

    private readonly SKPaint _snowPaint = new() { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _snowPilePaint = new() { Color = new SKColor(248, 251, 255, 250), IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _snowOutlinePaint = new() { Color = new SKColor(180, 208, 238, 225), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f };
    private readonly SKPaint _iciclePaint = new() { Color = new SKColor(200, 235, 255, 205), IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _coalPaint = new() { Color = new SKColor(32, 35, 42), IsAntialias = true };
    private readonly SKPaint _branchPaint = new() { Color = new SKColor(92, 58, 33), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.6f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _wirePaint = new() { Color = new SKColor(45, 55, 48, 200), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f };

    private static readonly SKColor[] BulbColors =
    {
        new(255, 195, 65), new(255, 75, 75), new(255, 225, 115), new(75, 220, 125), new(85, 190, 255)
    };

    public OverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Engine = new SnowPhysicsEngine((int)Width, (int)Height, _settings);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Win32Interop.MakeWindowClickThrough(_hwnd);
        _stopwatch.Start();
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double now = _stopwatch.Elapsed.TotalSeconds;
        float dt = (float)Math.Clamp(now - _lastFrameTime, 0.001, 0.05);
        _lastFrameTime = now;

        if (now - _lastWindowScanTime > 0.033)
        {
            Win32Interop.ForceGameOverlayCompatibility(_hwnd, (int)Width, (int)Height); Engine.UpdateWindows(_hwnd);
            _lastWindowScanTime = now;
        }

        Engine.Step(dt);
        SkiaCanvas.InvalidateVisual();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (!_isInitializedSize && e.Info.Width > 0 && e.Info.Height > 0)
        {
            _isInitializedSize = true;
            Engine = new SnowPhysicsEngine(e.Info.Width, e.Info.Height, _settings);
            Win32Interop.ForceGameOverlayCompatibility(_hwnd, (int)Width, (int)Height); Engine.UpdateWindows(_hwnd);
        }

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (Engine.IsPausedByFullscreen) return;

        float timeSec = (float)_stopwatch.Elapsed.TotalSeconds;

        if (Engine.Santa.IsActive) DrawSantaAndReindeer(canvas, Engine.Santa);

        foreach (var surface in Engine.Surfaces)
        {
            if (_settings.ShowStringLights) DrawStringLights(canvas, surface.Bounds.Left, surface.Bounds.Top, surface.Bounds.Width, timeSec);
            if (_settings.ShowTrees)
                foreach (float offsetX in surface.TreeOffsetsX)
                    DrawMiniPineTree(canvas, surface.Bounds.Left + offsetX, surface.Bounds.Top);

            DrawSnowMound(canvas, surface.Bounds.Left, surface.Bounds.Top + 2, surface.SnowHeights);

            if (_settings.ShowIcicles) DrawWindowIcicles(canvas, surface);
        }

        foreach (var gift in Engine.GiftBoxes) DrawGiftBox(canvas, gift.X, Engine.GroundY, gift.W, gift.H, gift.ColorIdx);
        foreach (var snowman in Engine.Snowmen) DrawSnowman(canvas, snowman, timeSec);
        foreach (var cookie in Engine.Cookies) DrawGingerbreadMan(canvas, cookie);
        foreach (var ball in Engine.Snowballs) DrawSnowball(canvas, ball);

        foreach (var part in Engine.Particles)
        {
            var col = part.DriftSpeed == 2f ? new SKColor(190, 232, 255) : SKColors.White;
            _snowPaint.Color = col.WithAlpha((byte)(Math.Clamp(part.Opacity, 0f, 1f) * 255));
            canvas.DrawCircle(part.X, part.Y, part.Radius, _snowPaint);
        }

        if (Engine.ActiveSnowball != null)
        {
            bool canStack = Engine.CanStackOn(Engine.ActiveSnowball, out var targetBall, out _);
            if (targetBall != null)
            {
                using var ind = new SKPaint { Color = canStack ? new SKColor(60, 220, 110, 210) : new SKColor(240, 65, 65, 210), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };
                canvas.DrawCircle(targetBall.X, targetBall.Y, targetBall.Radius + 4f, ind);
            }
            DrawSnowball(canvas, Engine.ActiveSnowball);
        }

        DrawSnowMound(canvas, 0, Engine.GroundY, Engine.FloorSnow);
        if (_settings.ShowStringLights) DrawBottomWarmGlowLights(canvas, timeSec);
        if (Engine.PlowTruck.IsActive) DrawSnowplowTruck(canvas, Engine.PlowTruck, timeSec);

        foreach (var trail in Engine.CursorTrail)
        {
            _snowPaint.Color = new SKColor(255, 235, 165, (byte)(trail.Alpha * 220));
            canvas.DrawCircle(trail.X, trail.Y, trail.Size, _snowPaint);
        }

        foreach (var flake in Engine.Flakes)
        {
            _snowPaint.Color = SKColors.White.WithAlpha((byte)(flake.Opacity * 255));
            canvas.DrawCircle(flake.X, flake.Y, flake.Radius, _snowPaint);
        }
    }

    private void DrawWindowIcicles(SKCanvas canvas, WindowSurface s)
    {
        for (int i = 2; i < s.IcicleLengths.Length - 2; i += 4)
        {
            float len = s.IcicleLengths[i];
            if (len < 3f) continue;

            float x = s.Bounds.Left + i * WindowSurface.BucketSize;
            float y = s.Bounds.Bottom;
            float halfW = Math.Clamp(len * 0.16f, 1.5f, 3.8f);

            using var tri = new SKPath();
            tri.MoveTo(x - halfW, y);
            tri.LineTo(x + halfW, y);
            tri.LineTo(x, y + len);
            tri.Close();
            canvas.DrawPath(tri, _iciclePaint);
        }
    }

    private void DrawStringLights(SKCanvas canvas, float startX, float topY, float width, float time)
    {
        if (width < 60) return;
        float spacing = 18f;
        int bulbCount = (int)(width / spacing);
        using var wirePath = new SKPath();
        wirePath.MoveTo(startX + 4, topY + 2);

        for (int i = 0; i < bulbCount; i++)
        {
            float x1 = startX + 4 + i * spacing;
            float x2 = Math.Min(startX + width - 4, x1 + spacing);
            wirePath.QuadTo((x1 + x2) * 0.5f, topY + 10f, x2, topY + 2f);
        }
        canvas.DrawPath(wirePath, _wirePaint);

        using var outerGlow = new SKPaint { IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7.5f) };
        using var bulbPaint = new SKPaint { IsAntialias = true };

        for (int i = 0; i < bulbCount; i++)
        {
            float bx = startX + 4 + (i + 0.5f) * spacing;
            if (bx > startX + width - 8) break;
            float by = topY + 8.5f;

            var baseColor = BulbColors[i % BulbColors.Length];
            float pulse = 0.72f + 0.28f * MathF.Sin(time * 3.8f + i * 1.15f);

            outerGlow.Color = baseColor.WithAlpha((byte)(pulse * 170));
            canvas.DrawCircle(bx, by, 9.5f, outerGlow);

            bulbPaint.Color = baseColor.WithAlpha((byte)(pulse * 255));
            canvas.DrawOval(bx, by, 2.5f, 3.8f, bulbPaint);
            canvas.DrawCircle(bx, by - 0.5f, 1.1f, _snowPaint);
        }
    }

    private void DrawBottomWarmGlowLights(SKCanvas canvas, float time)
    {
        float startX = Math.Max(20f, Engine.ScreenWidth - 330f);
        using var outerGlow = new SKPaint { IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f) };
        using var bulbPaint = new SKPaint { IsAntialias = true };

        int idx = 0;
        for (float x = startX; x < Engine.ScreenWidth - 20f; x += 22f)
        {
            float y = Engine.GroundY - 3f + MathF.Sin(idx * 0.9f) * 2.5f;
            var color = BulbColors[idx % BulbColors.Length];
            float pulse = 0.7f + 0.3f * MathF.Sin(time * 3.2f + idx);

            outerGlow.Color = color.WithAlpha((byte)(pulse * 165));
            canvas.DrawCircle(x, y, 9f, outerGlow);
            bulbPaint.Color = color.WithAlpha(255);
            canvas.DrawCircle(x, y, 2.4f, bulbPaint);
            idx++;
        }
    }

    private void DrawGiftBox(SKCanvas canvas, float x, float baseY, float w, float h, int colorIdx)
    {
        using var boxPaint = new SKPaint { Color = colorIdx == 0 ? new SKColor(210, 40, 50) : new SKColor(38, 145, 78), IsAntialias = true };
        using var ribbonPaint = new SKPaint { Color = new SKColor(248, 205, 70), IsAntialias = true };

        canvas.DrawRoundRect(x - w / 2f, baseY - h, w, h, 3, 3, boxPaint);
        canvas.DrawRoundRect(x - w / 2f - 1.5f, baseY - h - 3, w + 3, 4.5f, 2, 2, boxPaint);
        canvas.DrawRect(x - 2f, baseY - h - 3, 4f, h + 3, ribbonPaint);
        canvas.DrawRect(x - w / 2f, baseY - h * 0.55f, w, 3.5f, ribbonPaint);
        canvas.DrawCircle(x - 3f, baseY - h - 5, 3f, ribbonPaint);
        canvas.DrawCircle(x + 3f, baseY - h - 5, 3f, ribbonPaint);
    }

    private void DrawSnowplowTruck(SKCanvas canvas, SnowplowTruck truck, float time)
    {
        canvas.Save();
        canvas.Translate(truck.X, truck.Y);

        using var bodyPaint = new SKPaint { Color = new SKColor(248, 192, 32), IsAntialias = true };
        using var darkTrim = new SKPaint { Color = new SKColor(42, 46, 54), IsAntialias = true };
        using var cabWindow = new SKPaint { Color = new SKColor(175, 220, 250), IsAntialias = true };
        using var bladePaint = new SKPaint { Color = new SKColor(225, 115, 25), IsAntialias = true };

        canvas.DrawRoundRect(-38, -28, 44, 18, 3, 3, bodyPaint);
        canvas.DrawRoundRect(4, -36, 26, 26, 5, 5, bodyPaint);
        canvas.DrawRoundRect(12, -32, 14, 12, 2.5f, 2.5f, cabWindow);

        using var bladePath = new SKPath();
        bladePath.MoveTo(30, -18); bladePath.QuadTo(46, -12, 44, 0);
        bladePath.LineTo(34, 0); bladePath.LineTo(28, -14); bladePath.Close();
        canvas.DrawPath(bladePath, bladePaint);

        float pulse = 0.5f + 0.5f * MathF.Sin(time * 14f);
        using var beaconGlow = new SKPaint { Color = new SKColor(255, 140, 20, (byte)(pulse * 200)), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f), IsAntialias = true };
        canvas.DrawCircle(15, -40, 8f, beaconGlow);
        using var beaconCore = new SKPaint { Color = new SKColor(255, 165, 30), IsAntialias = true };
        canvas.DrawRoundRect(12, -42, 6, 6, 2, 2, beaconCore);

        foreach (float wx in new[] { -28f, -13f, 2f, 18f })
        {
            canvas.DrawCircle(wx, -6, 6.5f, darkTrim);
            canvas.DrawCircle(wx, -6, 3.0f, _snowPilePaint);
        }
        canvas.Restore();
    }

    private void DrawSantaAndReindeer(SKCanvas canvas, SantaSleigh santa)
    {
        using var trailPaint = new SKPaint { IsAntialias = true };
        foreach (var p in santa.Trail)
        {
            trailPaint.Color = new SKColor(255, 240, 175, (byte)(p.Alpha * 230));
            canvas.DrawCircle(p.X, p.Y, p.Size, trailPaint);
        }

        canvas.Save();
        canvas.Translate(santa.X, santa.Y); canvas.Scale(santa.Direction, 1);

        using var reinPaint = new SKPaint { Color = new SKColor(220, 185, 110, 200), StrokeWidth = 1.3f, IsAntialias = true };
        canvas.DrawLine(15, -4, 78, -6, reinPaint);

        using var deerPaint = new SKPaint { Color = new SKColor(145, 92, 52), IsAntialias = true };
        for (int d = 0; d < 2; d++)
        {
            float dx = 48 + d * 32;
            float bounce = MathF.Sin(santa.WavePhase * 2.5f + d) * 3f;
            canvas.DrawOval(dx, -6 + bounce, 10, 5.5f, deerPaint);
            canvas.DrawCircle(dx + 9, -11 + bounce, 4.5f, deerPaint);
            canvas.DrawLine(dx + 8, -15 + bounce, dx + 6, -21 + bounce, _branchPaint);
            canvas.DrawLine(dx + 10, -15 + bounce, dx + 13, -21 + bounce, _branchPaint);
            if (d == 1)
            {
                using var rudolphNose = new SKPaint { Color = SKColors.Red, IsAntialias = true };
                canvas.DrawCircle(dx + 13.5f, -11 + bounce, 2.0f, rudolphNose);
            }
        }

        using var sleighPaint = new SKPaint { Color = new SKColor(205, 35, 45), IsAntialias = true };
        using var goldRunner = new SKPaint { Color = new SKColor(245, 200, 75), StrokeWidth = 2.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
        canvas.DrawRoundRect(-22, -12, 36, 14, 5, 5, sleighPaint);
        canvas.DrawLine(-26, 4, 20, 4, goldRunner);
        canvas.DrawArc(new SKRect(14, -4, 24, 4), 0, 90, false, goldRunner);
        canvas.DrawCircle(-4, -17, 7.5f, sleighPaint);

        using var facePaint = new SKPaint { Color = new SKColor(255, 218, 190), IsAntialias = true };
        canvas.DrawCircle(-3, -26, 4.5f, facePaint);
        canvas.DrawCircle(-1, -24, 4.2f, _snowPaint);
        using var hatTri = new SKPath();
        hatTri.MoveTo(-8, -29); hatTri.LineTo(2, -29); hatTri.LineTo(-9, -36); hatTri.Close();
        canvas.DrawPath(hatTri, sleighPaint);
        canvas.DrawCircle(-9.5f, -36, 2.0f, _snowPaint);
        canvas.Restore();
    }

    private void DrawSnowball(SKCanvas canvas, Snowball ball)
    {
        canvas.DrawCircle(ball.X, ball.Y, ball.Radius, _snowPilePaint);
        canvas.DrawCircle(ball.X, ball.Y, ball.Radius, _snowOutlinePaint);
        canvas.DrawCircle(ball.X - ball.Radius * 0.3f, ball.Y - ball.Radius * 0.3f, ball.Radius * 0.22f, _snowPaint);
    }

    private void DrawSnowMound(SKCanvas canvas, float startX, float baseY, float[] heights)
    {
        if (heights.Length < 3) return;
        using var fillPath = new SKPath();
        using var ridgePath = new SKPath();

        float firstY = baseY - heights[0];
        fillPath.MoveTo(startX, baseY);
        fillPath.LineTo(startX, firstY);
        ridgePath.MoveTo(startX, firstY);

        for (int i = 1; i < heights.Length - 1; i++)
        {
            float cx = startX + i * WindowSurface.BucketSize;
            float cy = baseY - heights[i];
            float nx = startX + (i + 1) * WindowSurface.BucketSize;
            float ny = baseY - heights[i + 1];
            fillPath.QuadTo(cx, cy, (cx + nx) * 0.5f, (cy + ny) * 0.5f);
            ridgePath.QuadTo(cx, cy, (cx + nx) * 0.5f, (cy + ny) * 0.5f);
        }

        float lastX = startX + (heights.Length - 1) * WindowSurface.BucketSize;
        float lastY = baseY - heights[^1];
        fillPath.LineTo(lastX, lastY);
        fillPath.LineTo(lastX, baseY);
        fillPath.Close();
        ridgePath.LineTo(lastX, lastY);

        using var snowGradient = SKShader.CreateLinearGradient(
            new SKPoint(0, baseY - 75f), new SKPoint(0, baseY),
            new[] { SKColors.White, new SKColor(232, 242, 255, 248), new SKColor(208, 224, 245, 245) },
            new[] { 0.0f, 0.65f, 1.0f }, SKShaderTileMode.Clamp);

        using var fillPaint = new SKPaint { Shader = snowGradient, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawPath(fillPath, fillPaint);
        canvas.DrawPath(ridgePath, _snowOutlinePaint);
    }

    private void DrawSnowman(SKCanvas canvas, Snowman s, float time)
    {
        canvas.Save();
        float sway = MathF.Sin(s.WobblePhase) * 1.4f;
        canvas.Translate(s.X, s.BaseY);
        canvas.Scale(s.Scale);

        var scarfCol = s.ScarfTheme switch
        {
            SnowmanScarfTheme.EmeraldGreen => new SKColor(38, 155, 82),
            SnowmanScarfTheme.RoyalGold => new SKColor(235, 175, 35),
            SnowmanScarfTheme.FrostBlue => new SKColor(55, 155, 235),
            _ => new SKColor(210, 42, 52)
        };
        using var scarfPaint = new SKPaint { Color = scarfCol, IsAntialias = true };

        void DrawShadedSphere(float cx, float cy, float r)
        {
            using var shader = SKShader.CreateRadialGradient(
                new SKPoint(cx - r * 0.32f, cy - r * 0.32f), r * 1.35f,
                new[] { SKColors.White, new SKColor(238, 245, 255), new SKColor(195, 214, 238) },
                new[] { 0.0f, 0.65f, 1.0f }, SKShaderTileMode.Clamp);
            using var sp = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawCircle(cx, cy, r, sp);
            canvas.DrawCircle(cx, cy, r, _snowOutlinePaint);
        }

        DrawShadedSphere(0, -23, 24);

        canvas.DrawLine(-15 + sway * 0.5f, -58, -36 + sway * 0.4f, -68, _branchPaint);
        float armWave = MathF.Sin(s.WobblePhase * 1.4f) * 3f;
        float handX = 36 + sway * 0.4f;
        float handY = -67 + armWave;
        canvas.DrawLine(15 + sway * 0.5f, -58, handX, handY, _branchPaint);

        if (s.Prop == SnowmanPropType.Mittens)
        {
            canvas.DrawCircle(-36 + sway * 0.4f, -68, 4.5f, scarfPaint);
            canvas.DrawCircle(handX, handY, 4.5f, scarfPaint);
        }
        else if (s.Prop == SnowmanPropType.Broom)
        {
            canvas.DrawLine(handX, -2, handX + 4, -85, _branchPaint);
            using var straw = new SKPaint { Color = new SKColor(225, 185, 85), IsAntialias = true };
            using var bh = new SKPath();
            bh.MoveTo(handX - 1, -22); bh.LineTo(handX - 9, 0); bh.LineTo(handX + 8, 0); bh.Close();
            canvas.DrawPath(bh, straw);
        }
        else if (s.Prop == SnowmanPropType.CandyCane)
        {
            using var cane = new SKPaint { Color = new SKColor(215, 45, 55), StrokeWidth = 3.5f, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
            canvas.DrawLine(handX, -15, handX, -76, cane);
            canvas.DrawArc(new SKRect(handX, -83, handX + 11, -71), 180, 180, false, cane);
        }
        else if (s.Prop == SnowmanPropType.CocoaMug)
        {
            canvas.DrawRoundRect(handX - 4, handY - 7, 9, 10, 2, 2, scarfPaint);
            using var steam = new SKPaint { Color = new SKColor(255, 255, 255, 170), StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, IsAntialias = true };
            canvas.DrawLine(handX, handY - 10, handX + MathF.Sin(time * 4f) * 2f, handY - 17, steam);
        }
        else if (s.Prop == SnowmanPropType.Gift)
        {
            DrawGiftBox(canvas, handX, handY + 12, 13f, 12f, 0);
        }

        float midX = sway * 0.5f;
        DrawShadedSphere(midX, -56, 17.5f);
        foreach (float btnY in new[] { -62f, -54f, -46f }) canvas.DrawCircle(midX, btnY, 2.3f, _coalPaint);

        using var bodyLightPath = new SKPath();
        bodyLightPath.MoveTo(midX - 15, -62); bodyLightPath.QuadTo(midX, -52, midX + 16, -54);
        bodyLightPath.QuadTo(midX + 20, -40, -19, -34); bodyLightPath.QuadTo(0, -20, 22, -22);
        canvas.DrawPath(bodyLightPath, _wirePaint);

        (float lx, float ly)[] lightPositions =
        {
            (midX - 11, -59), (midX + 3, -55), (midX + 14, -52),
            (midX + 6, -42), (-7, -36), (-18, -32), (-6, -25), (8, -22), (19, -21)
        };

        using var glowPaint = new SKPaint { IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4.5f) };
        using var bulbPaint = new SKPaint { IsAntialias = true };
        for (int i = 0; i < lightPositions.Length; i++)
        {
            var (lx, ly) = lightPositions[i];
            var baseColor = BulbColors[i % BulbColors.Length];
            float pulse = 0.65f + 0.35f * MathF.Sin(time * 4.2f + i * 1.3f + s.X * 0.05f);
            glowPaint.Color = baseColor.WithAlpha((byte)(pulse * 165));
            canvas.DrawCircle(lx, ly, 6.0f, glowPaint);
            bulbPaint.Color = baseColor.WithAlpha((byte)(pulse * 255));
            canvas.DrawCircle(lx, ly, 2.3f, bulbPaint);
        }

        float headX = sway, headY = -82;
        DrawShadedSphere(headX, headY, 13.5f);

        canvas.DrawRoundRect(new SKRect(headX - 13.5f, -72, headX + 13.5f, -65), 3.5f, 3.5f, scarfPaint);
        using var tailPath = new SKPath();
        tailPath.MoveTo(headX + 4, -67); tailPath.QuadTo(headX + 12 + sway, -58, headX + 9 + sway * 1.3f, -49);
        tailPath.LineTo(headX + 2 + sway * 1.3f, -50); tailPath.QuadTo(headX + 5 + sway, -58, headX - 1, -67);
        tailPath.Close();
        canvas.DrawPath(tailPath, scarfPaint);

        canvas.DrawCircle(headX - 4.5f, headY - 3.5f, 2.0f, _coalPaint);
        canvas.DrawCircle(headX + 4.5f, headY - 3.5f, 2.0f, _coalPaint);
        for (int m = -2; m <= 2; m++) canvas.DrawCircle(headX + m * 2.2f, headY + 5.2f - (m * m) * 0.35f, 1.0f, _coalPaint);

        using var carrotPaint = new SKPaint { Color = new SKColor(245, 125, 25), IsAntialias = true };
        using var nosePath = new SKPath();
        nosePath.MoveTo(headX, headY - 1.5f); nosePath.QuadTo(headX + 7, headY - 1f, headX + 13.5f, headY + 1.2f);
        nosePath.QuadTo(headX + 7, headY + 3.2f, headX, headY + 3.0f); nosePath.Close();
        canvas.DrawPath(nosePath, carrotPaint);

        canvas.Save();
        canvas.Translate(headX + 1, headY - 13);
        canvas.RotateDegrees(6f);

        if (s.Hat == SnowmanHatType.SantaHat)
        {
            using var tri = new SKPath();
            tri.MoveTo(-12, 0); tri.LineTo(12, 0); tri.QuadTo(4, -18, -10, -21); tri.Close();
            canvas.DrawPath(tri, scarfPaint);
            canvas.DrawRoundRect(-13, -3, 26, 5, 2.5f, 2.5f, _snowPaint);
            canvas.DrawCircle(-11, -21, 3.2f, _snowPaint);
        }
        else if (s.Hat == SnowmanHatType.Beanie)
        {
            canvas.DrawArc(new SKRect(-12, -16, 12, 8), 180, 180, true, scarfPaint);
            canvas.DrawRoundRect(-13, -3, 26, 4.5f, 2, 2, _snowPaint);
            canvas.DrawCircle(0, -18, 3.5f, _snowPaint);
        }
        else if (s.Hat == SnowmanHatType.Earmuffs)
        {
            canvas.DrawArc(new SKRect(-15, -4, 15, 22), 180, 180, false, _branchPaint);
            canvas.DrawCircle(-15, 11, 5.5f, scarfPaint);
            canvas.DrawCircle(15, 11, 5.5f, scarfPaint);
        }
        else
        {
            canvas.DrawRoundRect(-16, -3, 32, 4.5f, 2, 2, _coalPaint);
            canvas.DrawRoundRect(-10, -20, 20, 18, 2.5f, 2.5f, _coalPaint);
            canvas.DrawRect(-10, -7, 20, 3.5f, scarfPaint);
        }

        canvas.Restore();
        canvas.Restore();
    }

    private void DrawGingerbreadMan(SKCanvas canvas, GingerbreadMan c)
    {
        canvas.Save();
        float wave = MathF.Sin(c.WavePhase) * 4f;
        canvas.Translate(c.X, c.BaseY);
        canvas.Scale(c.Scale);

        using var cookiePaint = new SKPaint { Color = new SKColor(184, 107, 53), IsAntialias = true };
        using var strokePaint = new SKPaint { Color = new SKColor(135, 72, 30), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f };
        using var icingPaint = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
        using var redBtn = new SKPaint { Color = new SKColor(210, 42, 52), IsAntialias = true };
        using var greenBtn = new SKPaint { Color = new SKColor(45, 165, 85), IsAntialias = true };

        canvas.DrawRoundRect(-11, -22, 8, 22, 4, 4, cookiePaint);
        canvas.DrawRoundRect(3, -22, 8, 22, 4, 4, cookiePaint);
        canvas.DrawRoundRect(-21, -42, 14, 7, 3.5f, 3.5f, cookiePaint);

        canvas.Save();
        canvas.Translate(10, -39);
        canvas.RotateDegrees(-25 + wave * 3f);
        canvas.DrawRoundRect(0, -3.5f, 15, 7, 3.5f, 3.5f, cookiePaint);
        canvas.Restore();

        canvas.DrawRoundRect(-12, -45, 24, 27, 8, 8, cookiePaint);
        canvas.DrawRoundRect(-12, -45, 24, 27, 8, 8, strokePaint);
        canvas.DrawCircle(0, -56, 12, cookiePaint);
        canvas.DrawCircle(0, -56, 12, strokePaint);

        canvas.DrawCircle(-4, -58, 2f, _snowPaint);
        canvas.DrawCircle(4, -58, 2f, _snowPaint);
        canvas.DrawArc(new SKRect(-5, -56, 5, -49), 15, 150, false, icingPaint);

        canvas.DrawCircle(0, -38, 2.5f, redBtn);
        canvas.DrawCircle(0, -30, 2.5f, greenBtn);
        canvas.DrawCircle(0, -45, 2.5f, greenBtn);
        canvas.Restore();
    }

    private void DrawMiniPineTree(SKCanvas canvas, float x, float baseY)
    {
        using var trunkPaint = new SKPaint { Color = new SKColor(92, 64, 51), IsAntialias = true };
        canvas.DrawRect(x - 2, baseY - 6, 4, 6, trunkPaint);

        using var leafPaint = new SKPaint { Color = new SKColor(34, 110, 62), IsAntialias = true };
        for (int tier = 0; tier < 3; tier++)
        {
            float tierBottom = baseY - 5 - (tier * 7);
            using var triangle = new SKPath();
            triangle.MoveTo(x, tierBottom - 11);
            triangle.LineTo(x - (10 - tier * 2.2f), tierBottom);
            triangle.LineTo(x + (10 - tier * 2.2f), tierBottom);
            triangle.Close();
            canvas.DrawPath(triangle, leafPaint);
        }

        SKColor[] lightColors = { SKColors.Red, SKColors.Gold, SKColors.DeepSkyBlue };
        for (int i = 0; i < 3; i++)
        {
            using var lp = new SKPaint { Color = lightColors[i], IsAntialias = true };
            canvas.DrawCircle(x + (i - 1) * 4f, baseY - 10 - (i * 5f), 1.6f, lp);
        }
    }
}

