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
    private readonly SKPaint _coalPaint = new() { Color = new SKColor(32, 35, 42), IsAntialias = true };
    private readonly SKPaint _scarfMain = new() { Color = new SKColor(210, 42, 52), IsAntialias = true };
    private readonly SKPaint _scarfStripe = new() { Color = new SKColor(245, 215, 110), IsAntialias = true };
    private readonly SKPaint _branchPaint = new() { Color = new SKColor(92, 58, 33), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.6f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _wirePaint = new() { Color = new SKColor(45, 55, 48, 190), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f };

    private static readonly SKColor[] BulbColors =
    {
        new(255, 85, 85),
        new(255, 210, 75),
        new(80, 225, 130),
        new(95, 195, 255)
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
            Engine.UpdateWindows(_hwnd);
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
            Engine.UpdateWindows(_hwnd);
        }

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Engine.IsPausedByFullscreen) return;

        float timeSec = (float)_stopwatch.Elapsed.TotalSeconds;

        if (Engine.Santa.IsActive)
        {
            DrawSantaAndReindeer(canvas, Engine.Santa);
        }

        foreach (var surface in Engine.Surfaces)
        {
            if (_settings.ShowStringLights)
            {
                DrawStringLights(canvas, surface.Bounds.Left, surface.Bounds.Top, surface.Bounds.Width, timeSec);
            }

            if (_settings.ShowTrees)
            {
                foreach (float offsetX in surface.TreeOffsetsX)
                {
                    DrawMiniPineTree(canvas, surface.Bounds.Left + offsetX, surface.Bounds.Top);
                }
            }

            DrawSnowMound(canvas, surface.Bounds.Left, surface.Bounds.Top + 2, surface.SnowHeights);
        }

        foreach (var snowman in Engine.Snowmen)
        {
            DrawSnowman(canvas, snowman, timeSec);
        }

        foreach (var cookie in Engine.Cookies)
        {
            DrawGingerbreadMan(canvas, cookie);
        }

        foreach (var ball in Engine.Snowballs)
        {
            DrawSnowball(canvas, ball);
        }

        // Parçalanan kartopu parçacıklarını çiz
        foreach (var part in Engine.SplatParticles)
        {
            _snowPaint.Color = SKColors.White.WithAlpha((byte)(Math.Clamp(part.Opacity, 0f, 1f) * 255));
            canvas.DrawCircle(part.X, part.Y, part.Radius, _snowPaint);
        }

        if (Engine.ActiveSnowball != null)
        {
            bool canStack = Engine.CanStackOn(Engine.ActiveSnowball, out var targetBall, out _);
            if (targetBall != null)
            {
                using var indicatorPaint = new SKPaint
                {
                    Color = canStack ? new SKColor(60, 220, 110, 210) : new SKColor(240, 65, 65, 210),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2.5f
                };
                canvas.DrawCircle(targetBall.X, targetBall.Y, targetBall.Radius + 4f, indicatorPaint);
            }

            DrawSnowball(canvas, Engine.ActiveSnowball);
        }

        DrawSnowMound(canvas, 0, Engine.GroundY, Engine.FloorSnow);

        foreach (var flake in Engine.Flakes)
        {
            _snowPaint.Color = SKColors.White.WithAlpha((byte)(flake.Opacity * 255));
            canvas.DrawCircle(flake.X, flake.Y, flake.Radius, _snowPaint);
        }
    }

    private void DrawStringLights(SKCanvas canvas, float startX, float topY, float width, float time)
    {
        if (width < 80) return;

        float spacing = 26f;
        int bulbCount = (int)(width / spacing);
        using var wirePath = new SKPath();
        wirePath.MoveTo(startX + 6, topY + 2);

        for (int i = 0; i < bulbCount; i++)
        {
            float x1 = startX + 6 + i * spacing;
            float x2 = Math.Min(startX + width - 6, x1 + spacing);
            float midX = (x1 + x2) * 0.5f;
            wirePath.QuadTo(midX, topY + 9f, x2, topY + 2f);
        }
        canvas.DrawPath(wirePath, _wirePaint);

        using var glowPaint = new SKPaint { IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4.5f) };
        using var bulbPaint = new SKPaint { IsAntialias = true };

        for (int i = 0; i < bulbCount; i++)
        {
            float bx = startX + 6 + (i + 0.5f) * spacing;
            if (bx > startX + width - 10) break;
            float by = topY + 7.5f;

            var baseColor = BulbColors[i % BulbColors.Length];
            float pulse = 0.65f + 0.35f * MathF.Sin(time * 3.5f + i * 1.1f);
            byte alpha = (byte)(pulse * 255);

            glowPaint.Color = baseColor.WithAlpha((byte)(pulse * 130));
            canvas.DrawCircle(bx, by, 6.5f, glowPaint);

            bulbPaint.Color = baseColor.WithAlpha(alpha);
            canvas.DrawOval(bx, by, 2.3f, 3.4f, bulbPaint);
        }
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
        canvas.Translate(santa.X, santa.Y);

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
        hatTri.MoveTo(-8, -29);
        hatTri.LineTo(2, -29);
        hatTri.LineTo(-9, -36);
        hatTri.Close();
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

        float firstX = startX;
        float firstY = baseY - heights[0];

        fillPath.MoveTo(firstX, baseY);
        fillPath.LineTo(firstX, firstY);
        ridgePath.MoveTo(firstX, firstY);

        for (int i = 1; i < heights.Length - 1; i++)
        {
            float currX = startX + i * WindowSurface.BucketSize;
            float currY = baseY - heights[i];

            float nextX = startX + (i + 1) * WindowSurface.BucketSize;
            float nextY = baseY - heights[i + 1];

            float midX = (currX + nextX) * 0.5f;
            float midY = (currY + nextY) * 0.5f;

            fillPath.QuadTo(currX, currY, midX, midY);
            ridgePath.QuadTo(currX, currY, midX, midY);
        }

        float lastX = startX + (heights.Length - 1) * WindowSurface.BucketSize;
        float lastY = baseY - heights[^1];
        fillPath.LineTo(lastX, lastY);
        fillPath.LineTo(lastX, baseY);
        fillPath.Close();

        ridgePath.LineTo(lastX, lastY);

        using var snowGradient = SKShader.CreateLinearGradient(
            new SKPoint(0, baseY - 75f),
            new SKPoint(0, baseY),
            new[] { SKColors.White, new SKColor(232, 242, 255, 248), new SKColor(208, 224, 245, 245) },
            new[] { 0.0f, 0.65f, 1.0f },
            SKShaderTileMode.Clamp);

        using var gradientFillPaint = new SKPaint
        {
            Shader = snowGradient,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawPath(fillPath, gradientFillPaint);
        canvas.DrawPath(ridgePath, _snowOutlinePaint);
    }

    private void DrawSnowman(SKCanvas canvas, Snowman s, float time)
    {
        canvas.Save();
        float sway = MathF.Sin(s.WobblePhase) * 1.4f;
        canvas.Translate(s.X, s.BaseY);
        canvas.Scale(s.Scale);

        void DrawShadedSphere(float cx, float cy, float r)
        {
            using var shader = SKShader.CreateRadialGradient(
                new SKPoint(cx - r * 0.32f, cy - r * 0.32f),
                r * 1.35f,
                new[] { SKColors.White, new SKColor(238, 245, 255), new SKColor(195, 214, 238) },
                new[] { 0.0f, 0.65f, 1.0f },
                SKShaderTileMode.Clamp);

            using var spherePaint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawCircle(cx, cy, r, spherePaint);
            canvas.DrawCircle(cx, cy, r, _snowOutlinePaint);
        }

        DrawShadedSphere(0, -23, 24);

        canvas.DrawLine(-15 + sway * 0.5f, -58, -36 + sway * 0.4f, -68, _branchPaint);
        canvas.DrawLine(-30 + sway * 0.4f, -65, -35 + sway * 0.4f, -74, _branchPaint);
        canvas.DrawLine(-32 + sway * 0.4f, -66, -40 + sway * 0.4f, -63, _branchPaint);

        float armWave = MathF.Sin(s.WobblePhase * 1.4f) * 3f;
        canvas.DrawLine(15 + sway * 0.5f, -58, 36 + sway * 0.4f, -67 + armWave, _branchPaint);
        canvas.DrawLine(30 + sway * 0.4f, -64 + armWave * 0.8f, 36 + sway * 0.4f, -73 + armWave, _branchPaint);
        canvas.DrawLine(31 + sway * 0.4f, -65 + armWave * 0.8f, 40 + sway * 0.4f, -62 + armWave, _branchPaint);

        float midX = sway * 0.5f;
        DrawShadedSphere(midX, -56, 17.5f);

        foreach (float btnY in new[] { -62f, -54f, -46f })
        {
            canvas.DrawCircle(midX, btnY, 2.3f, _coalPaint);
        }

        using var bodyLightPath = new SKPath();
        bodyLightPath.MoveTo(midX - 15, -62);
        bodyLightPath.QuadTo(midX, -52, midX + 16, -54);
        bodyLightPath.QuadTo(midX + 20, -40, -19, -34);
        bodyLightPath.QuadTo(0, -20, 22, -22);
        canvas.DrawPath(bodyLightPath, _wirePaint);

        (float lx, float ly)[] lightPositions =
        {
            (midX - 11, -59), (midX + 3, -55), (midX + 14, -52),
            (midX + 6, -42), (-7, -36), (-18, -32),
            (-6, -25), (8, -22), (19, -21)
        };

        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4.0f)
        };
        using var bulbPaint = new SKPaint { IsAntialias = true };

        for (int i = 0; i < lightPositions.Length; i++)
        {
            var (lx, ly) = lightPositions[i];
            var baseColor = BulbColors[i % BulbColors.Length];
            float pulse = 0.6f + 0.4f * MathF.Sin(time * 4.2f + i * 1.3f + s.X * 0.05f);

            glowPaint.Color = baseColor.WithAlpha((byte)(pulse * 150));
            canvas.DrawCircle(lx, ly, 5.5f, glowPaint);

            bulbPaint.Color = baseColor.WithAlpha((byte)(pulse * 255));
            canvas.DrawCircle(lx, ly, 2.2f, bulbPaint);
        }

        float headX = sway;
        float headY = -82;
        DrawShadedSphere(headX, headY, 13.5f);

        canvas.DrawRoundRect(new SKRect(headX - 13.5f, -72, headX + 13.5f, -65), 3.5f, 3.5f, _scarfMain);
        using var tailPath = new SKPath();
        tailPath.MoveTo(headX + 4, -67);
        tailPath.QuadTo(headX + 12 + sway, -58, headX + 9 + sway * 1.3f, -49);
        tailPath.LineTo(headX + 2 + sway * 1.3f, -50);
        tailPath.QuadTo(headX + 5 + sway, -58, headX - 1, -67);
        tailPath.Close();
        canvas.DrawPath(tailPath, _scarfMain);
        canvas.DrawLine(headX + 3 + sway * 1.1f, -53, headX + 8.5f + sway * 1.1f, -52.5f, _scarfStripe);

        canvas.DrawCircle(headX - 4.5f, headY - 3.5f, 2.0f, _coalPaint);
        canvas.DrawCircle(headX + 4.5f, headY - 3.5f, 2.0f, _coalPaint);
        for (int m = -2; m <= 2; m++)
        {
            canvas.DrawCircle(headX + m * 2.2f, headY + 5.2f - (m * m) * 0.35f, 1.0f, _coalPaint);
        }

        using var carrotPaint = new SKPaint { Color = new SKColor(245, 125, 25), IsAntialias = true };
        using var nosePath = new SKPath();
        nosePath.MoveTo(headX, headY - 1.5f);
        nosePath.QuadTo(headX + 7, headY - 1f, headX + 13.5f, headY + 1.2f);
        nosePath.QuadTo(headX + 7, headY + 3.2f, headX, headY + 3.0f);
        nosePath.Close();
        canvas.DrawPath(nosePath, carrotPaint);

        canvas.Save();
        canvas.Translate(headX + 1, headY - 13);
        canvas.RotateDegrees(6f);
        canvas.DrawRoundRect(-16, -3, 32, 4.5f, 2, 2, _coalPaint);
        canvas.DrawRoundRect(-10, -20, 20, 18, 2.5f, 2.5f, _coalPaint);
        canvas.DrawRect(-10, -7, 20, 3.5f, _scarfMain);
        using var hatSnow = new SKPath();
        hatSnow.MoveTo(-10, -19);
        hatSnow.QuadTo(0, -23, 10, -19);
        hatSnow.QuadTo(3, -17, -10, -19);
        canvas.DrawPath(hatSnow, _snowPilePaint);
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

        using var greenBtn = new SKPaint { Color = new SKColor(45, 165, 85), IsAntialias = true };
        canvas.DrawCircle(0, -38, 2.5f, _scarfMain);
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
            float tierTop = tierBottom - 11;
            float halfWidth = 10 - (tier * 2.2f);

            using var triangle = new SKPath();
            triangle.MoveTo(x, tierTop);
            triangle.LineTo(x - halfWidth, tierBottom);
            triangle.LineTo(x + halfWidth, tierBottom);
            triangle.Close();
            canvas.DrawPath(triangle, leafPaint);
        }

        SKColor[] lightColors = { SKColors.Red, SKColors.Gold, SKColors.DeepSkyBlue };
        for (int i = 0; i < 3; i++)
        {
            using var lightPaint = new SKPaint { Color = lightColors[i], IsAntialias = true };
            canvas.DrawCircle(x + (i - 1) * 4f, baseY - 10 - (i * 5f), 1.6f, lightPaint);
        }
    }
}