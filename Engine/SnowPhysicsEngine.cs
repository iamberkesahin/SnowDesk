using System.Windows.Input;
using SnowDesk.Models;
using SnowDesk.Native;

namespace SnowDesk.Engine;

public class SnowPhysicsEngine
{
    private readonly Random _rng = new();
    private readonly List<Snowflake> _flakes = new();
    private readonly List<Snowflake> _splatParticles = new();
    private readonly Dictionary<IntPtr, WindowSurface> _surfaces = new();
    private readonly List<Snowman> _snowmen = new();
    private readonly List<GingerbreadMan> _cookies = new();
    private readonly List<Snowball> _snowballs = new();
    private readonly List<(float X, float W, float H, int ColorIdx)> _giftBoxes = new();
    private Snowball? _activeSnowball;

    private float[] _floorSnow;
    private float[] _floorProfile;
    private int _lastMouseX;
    private int _lastMouseY;
    private float _windOffset;
    private float _time;
    private float _santaTimer = 10f;

    public AppSettings Settings { get; }
    public SantaSleigh Santa { get; } = new();
    public SnowplowTruck PlowTruck { get; } = new();
    public bool IsPausedByFullscreen { get; set; }

    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }
    public float GroundY => ScreenHeight - 72f;

    public const float MinSnowballRadius = 4.5f;
    public const float MaxSnowballRadius = 22.0f;
    public const float MinBaseRadius = 15.0f;

    public IReadOnlyList<Snowflake> Flakes => _flakes;
    public IReadOnlyList<Snowflake> SplatParticles => _splatParticles;
    public IReadOnlyCollection<WindowSurface> Surfaces => _surfaces.Values;
    public IReadOnlyList<Snowman> Snowmen => _snowmen;
    public IReadOnlyList<GingerbreadMan> Cookies => _cookies;
    public IReadOnlyList<Snowball> Snowballs => _snowballs;
    public IReadOnlyList<(float X, float W, float H, int ColorIdx)> GiftBoxes => _giftBoxes;
    public Snowball? ActiveSnowball => _activeSnowball;
    public float[] FloorSnow => _floorSnow;

    public SnowPhysicsEngine(int width, int height, AppSettings settings)
    {
        ScreenWidth = width;
        ScreenHeight = height;
        Settings = settings;

        int buckets = Math.Max(1, width / WindowSurface.BucketSize);
        _floorSnow = new float[buckets];
        _floorProfile = new float[buckets];
        GenerateFloorProfile();

        InitializeFlakes();
        SpawnCornerDecorations();
    }

    private void GenerateFloorProfile()
    {
        float phase1 = (float)(_rng.NextDouble() * Math.PI * 2);
        float phase2 = (float)(_rng.NextDouble() * Math.PI * 2);
        for (int i = 0; i < _floorProfile.Length; i++)
        {
            float drift1 = MathF.Sin(i * 0.015f + phase1) * 0.55f;
            float drift2 = MathF.Cos(i * 0.038f + phase2) * 0.30f;
            float drift3 = MathF.Sin(i * 0.09f) * 0.12f;
            _floorProfile[i] = Math.Clamp(0.80f + drift1 + drift2 + drift3, 0.18f, 1.85f);
        }
    }

    public void SpawnCornerDecorations()
    {
        _snowmen.Clear();
        _cookies.Clear();
        _giftBoxes.Clear();

        if (ScreenWidth > 300)
        {
            _snowmen.Add(new Snowman
            {
                X = ScreenWidth - 95f,
                BaseY = GroundY,
                Scale = 1.05f,
                Hat = SnowmanHatType.SantaHat,
                Prop = SnowmanPropType.CandyCane
            });
            _cookies.Add(new GingerbreadMan { X = ScreenWidth - 165f, BaseY = GroundY, Scale = 0.95f, WavePhase = 0f });
            _cookies.Add(new GingerbreadMan { X = ScreenWidth - 220f, BaseY = GroundY, Scale = 0.80f, WavePhase = 1.8f });

            _giftBoxes.Add((ScreenWidth - 265f, 22f, 20f, 0));
            _giftBoxes.Add((ScreenWidth - 285f, 16f, 14f, 1));
            _giftBoxes.Add((75f, 24f, 22f, 0));
        }
    }

    public void StartSnowplowClear()
    {
        if (PlowTruck.IsActive) return;
        PlowTruck.IsActive = true;
        PlowTruck.X = -110f;
        PlowTruck.Y = GroundY;
        PlowTruck.SpeedX = 270f;
        _activeSnowball = null;
    }

    public void CycleSnowmanStyle()
    {
        var hats = Enum.GetValues<SnowmanHatType>();
        var props = Enum.GetValues<SnowmanPropType>();
        foreach (var s in _snowmen)
        {
            s.Hat = hats[_rng.Next(hats.Length)];
            s.Prop = props[_rng.Next(props.Length)];
        }
    }

    public void TriggerSantaFlyby()
    {
        Santa.IsActive = true;
        Santa.X = -160f;
        Santa.Y = Math.Clamp(ScreenHeight * 0.14f, 80f, 180f);
        Santa.SpeedX = 165f;
        Santa.WavePhase = 0f;
        Santa.Trail.Clear();
    }

    private void InitializeFlakes()
    {
        _flakes.Clear();
        for (int i = 0; i < Settings.TargetFlakeCount; i++)
        {
            _flakes.Add(CreateFlake(randomY: true));
        }
    }

    private Snowflake CreateFlake(bool randomY)
    {
        return new Snowflake
        {
            X = _rng.Next(0, ScreenWidth),
            Y = randomY ? _rng.Next(0, ScreenHeight) : -_rng.Next(5, 60),
            Radius = (float)(_rng.NextDouble() * 2.8 + 1.6),
            SpeedY = (float)(_rng.NextDouble() * 75 + 50),
            DriftPhase = (float)(_rng.NextDouble() * Math.PI * 2),
            DriftSpeed = (float)(_rng.NextDouble() * 1.6 + 0.8),
            Opacity = (float)(_rng.NextDouble() * 0.4 + 0.6)
        };
    }

    private void DropWindowSnowAsAvalanche(WindowSurface surface)
    {
        for (int i = 0; i < surface.SnowHeights.Length; i += 2)
        {
            float h = surface.SnowHeights[i];
            if (h > 2.0f)
            {
                float wx = surface.Bounds.Left + i * WindowSurface.BucketSize;
                float wy = surface.Bounds.Top - h * 0.4f;

                _splatParticles.Add(new Snowflake
                {
                    X = wx,
                    Y = wy,
                    Radius = Math.Clamp(h * 0.25f, 2.2f, 5.5f),
                    DriftPhase = (float)(_rng.NextDouble() * 40 - 20),
                    SpeedY = (float)(_rng.NextDouble() * 60 + 20),
                    Opacity = 1.0f,
                    DriftSpeed = 1f
                });
            }
        }
    }

    public void UpdateWindows(IntPtr overlayHwnd)
    {
        if (Settings.AutoPauseFullscreen)
        {
            IsPausedByFullscreen = Win32Interop.IsForegroundFullscreen(overlayHwnd, ScreenWidth, ScreenHeight);
            if (IsPausedByFullscreen) return;
        }
        else
        {
            IsPausedByFullscreen = false;
        }

        var activeWindows = Win32Interop.GetVisibleWindows(overlayHwnd);
        var activeHandles = new HashSet<IntPtr>();

        foreach (var (hwnd, bounds) in activeWindows)
        {
            activeHandles.Add(hwnd);

            if (_surfaces.TryGetValue(hwnd, out var surface))
            {
                surface.UpdateBounds(bounds, out float delta);
                if (delta > 3f)
                {
                    surface.ShedSnow(delta * 0.35f);
                }
            }
            else
            {
                var newSurface = new WindowSurface(hwnd, bounds);
                for (int i = 1; i < newSurface.SnowHeights.Length - 1; i++)
                {
                    newSurface.SnowHeights[i] = 4.5f * newSurface.OrganicMultiplier[i];
                }

                if (bounds.Width > 320 && _rng.NextDouble() < 0.75)
                {
                    int treeCount = _rng.Next(1, 4);
                    float baseOffset = _rng.Next(45, Math.Max(65, bounds.Width - 120));
                    for (int t = 0; t < treeCount; t++)
                    {
                        newSurface.TreeOffsetsX.Add(baseOffset + t * 24f);
                    }
                }
                _surfaces[hwnd] = newSurface;
            }
        }

        var removed = _surfaces.Keys.Where(k => !activeHandles.Contains(k)).ToList();
        foreach (var key in removed)
        {
            DropWindowSnowAsAvalanche(_surfaces[key]);
            _surfaces.Remove(key);
        }
    }

    public void Step(float dt)
    {
        if (IsPausedByFullscreen) return;

        _time += dt;
        _windOffset = MathF.Sin(_time * 0.4f) * 18f;

        UpdateSanta(dt);
        UpdateSnowplow(dt);

        // OTOMATİK KAR KÜREME TETİKLEYİCİSİ:
        // Ortalama kar kalınlığı kardan adamın bel hizasına (~42px) ulaşınca kamyon otomatik gelir!
        if (!PlowTruck.IsActive && _activeSnowball == null && _floorSnow.Length > 0)
        {
            float avgSnow = _floorSnow.Average();
            if (avgSnow >= 42f)
            {
                StartSnowplowClear();
            }
        }

        if (_flakes.Count < Settings.TargetFlakeCount)
        {
            int toAdd = Math.Min(4, Settings.TargetFlakeCount - _flakes.Count);
            for (int i = 0; i < toAdd; i++)
                _flakes.Add(CreateFlake(randomY: false));
        }

        Win32Interop.GetCursorPos(out var cursor);
        float mouseVx = (cursor.X - _lastMouseX) / dt;
        float mouseVy = (cursor.Y - _lastMouseY) / dt;
        float mouseSpeed = MathF.Sqrt((cursor.X - _lastMouseX) * (cursor.X - _lastMouseX) +
                                      (cursor.Y - _lastMouseY) * (cursor.Y - _lastMouseY));
        _lastMouseX = cursor.X;
        _lastMouseY = cursor.Y;

        bool isShiftHeld = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        HandleSnowballBuilder(cursor.X, cursor.Y, mouseVx, mouseVy, mouseSpeed, isShiftHeld);
        UpdateThrownSnowballs(dt);
        UpdateSplatParticles(dt);
        HandleWindowSnowDisturbance(cursor.X, cursor.Y);

        for (int idxFlake = _flakes.Count - 1; idxFlake >= 0; idxFlake--)
        {
            var flake = _flakes[idxFlake];

            flake.DriftPhase += flake.DriftSpeed * dt;
            flake.X += (MathF.Sin(flake.DriftPhase) * 28f + _windOffset) * dt;
            flake.Y += flake.SpeedY * dt;

            float dx = flake.X - cursor.X;
            float dy = flake.Y - cursor.Y;
            float distSq = dx * dx + dy * dy;
            if (distSq < 6400f && distSq > 1f)
            {
                float force = (1f - MathF.Sqrt(distSq) / 80f) * 0.15f;
                flake.X += mouseVx * force * dt;
                flake.Y += mouseVy * force * dt;
            }

            if (flake.X < 0) flake.X += ScreenWidth;
            if (flake.X >= ScreenWidth) flake.X -= ScreenWidth;

            bool landed = false;

            foreach (var surface in _surfaces.Values)
            {
                if (flake.X >= surface.Bounds.Left && flake.X <= surface.Bounds.Right)
                {
                    int bucket = Math.Clamp((int)((flake.X - surface.Bounds.Left) / WindowSurface.BucketSize), 0, surface.SnowHeights.Length - 1);
                    float maxDepthHere = WindowSurface.MaxSnowDepth * surface.OrganicMultiplier[bucket];
                    float topEdge = surface.Bounds.Top - surface.SnowHeights[bucket] * 0.4f;

                    if (flake.Y >= topEdge && flake.Y <= surface.Bounds.Top + 16f)
                    {
                        if (surface.SnowHeights[bucket] < maxDepthHere)
                        {
                            surface.SnowHeights[bucket] += flake.Radius * 0.65f * surface.OrganicMultiplier[bucket];
                        }
                        landed = true;
                        break;
                    }
                }
            }

            if (!landed && flake.Y >= GroundY - 130f)
            {
                int bucket = Math.Clamp((int)(flake.X / WindowSurface.BucketSize), 0, _floorSnow.Length - 1);
                float floorY = GroundY - _floorSnow[bucket];

                if (flake.Y >= floorY)
                {
                    float maxFloorHere = 85f * _floorProfile[bucket];
                    if (_floorSnow[bucket] < maxFloorHere)
                    {
                        float mult = _floorProfile[bucket];
                        for (int offset = -3; offset <= 3; offset++)
                        {
                            int idx = bucket + offset;
                            if (idx >= 0 && idx < _floorSnow.Length)
                            {
                                float weight = MathF.Exp(-(offset * offset) / 4.5f);
                                _floorSnow[idx] += flake.Radius * 0.32f * mult * weight;
                            }
                        }
                    }
                    landed = true;
                }
            }

            if (landed || flake.Y > ScreenHeight)
            {
                if (_flakes.Count > Settings.TargetFlakeCount)
                {
                    _flakes.RemoveAt(idxFlake);
                }
                else
                {
                    var reset = CreateFlake(randomY: false);
                    flake.X = reset.X;
                    flake.Y = reset.Y;
                    flake.Radius = reset.Radius;
                    flake.SpeedY = reset.SpeedY;
                }
            }
        }

        foreach (var surface in _surfaces.Values) surface.SmoothSnow();
        SmoothFloor();

        foreach (var snowman in _snowmen) snowman.WobblePhase += dt * 2.5f;
        foreach (var cookie in _cookies) cookie.WavePhase += dt * 3.2f;
    }

    private void UpdateSnowplow(float dt)
    {
        if (!PlowTruck.IsActive) return;

        PlowTruck.X += PlowTruck.SpeedX * dt;
        PlowTruck.WheelAngle += dt * 10f;

        float bladeX = PlowTruck.X + 42f;
        int bladeBucket = Math.Clamp((int)(bladeX / WindowSurface.BucketSize), 0, _floorSnow.Length - 1);

        for (int i = Math.Max(0, bladeBucket - 12); i <= bladeBucket; i++)
        {
            if (_floorSnow[i] > 0.5f)
            {
                if (_rng.NextDouble() < 0.35)
                {
                    _splatParticles.Add(new Snowflake
                    {
                        X = bladeX,
                        Y = GroundY - 8f,
                        Radius = (float)(_rng.NextDouble() * 2.5 + 1.5),
                        DriftPhase = (float)(_rng.NextDouble() * 110 + 40),
                        SpeedY = -(float)(_rng.NextDouble() * 130 + 50),
                        Opacity = 0.95f
                    });
                }
                _floorSnow[i] = 0f;
            }
        }

        _snowballs.RemoveAll(b => !b.IsThrown && b.X <= bladeX + 12f);

        foreach (var s in _surfaces.Values)
        {
            s.ShedSnow(dt * 14f);
        }

        if (PlowTruck.X > ScreenWidth + 140f)
        {
            PlowTruck.IsActive = false;
            Array.Clear(_floorSnow, 0, _floorSnow.Length);
        }
    }

    private void ShatterSnowball(float x, float y, float radius, float impactVx, float impactVy)
    {
        int pieceCount = (int)Math.Clamp(radius * 1.4f, 12, 32);
        for (int i = 0; i < pieceCount; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float speed = (float)(_rng.NextDouble() * 240 + 60);

            _splatParticles.Add(new Snowflake
            {
                X = x,
                Y = y,
                Radius = (float)(_rng.NextDouble() * 3.2 + 1.4),
                DriftPhase = MathF.Cos(angle) * speed - impactVx * 0.15f,
                SpeedY = MathF.Sin(angle) * speed - MathF.Abs(impactVy) * 0.25f - 80f,
                Opacity = 1.0f
            });
        }
    }

    private void UpdateSplatParticles(float dt)
    {
        for (int i = _splatParticles.Count - 1; i >= 0; i--)
        {
            var p = _splatParticles[i];
            p.SpeedY += 620f * dt;
            p.X += p.DriftPhase * dt;
            p.Y += p.SpeedY * dt;

            if (p.DriftSpeed < 0.5f)
                p.Opacity -= dt * 1.4f;

            if (p.Y >= GroundY)
            {
                int bucket = Math.Clamp((int)(p.X / WindowSurface.BucketSize), 0, _floorSnow.Length - 1);
                for (int k = -2; k <= 2; k++)
                {
                    int idx = bucket + k;
                    if (idx >= 0 && idx < _floorSnow.Length)
                        _floorSnow[idx] = Math.Min(90f, _floorSnow[idx] + p.Radius * 0.45f);
                }
                _splatParticles.RemoveAt(i);
                continue;
            }

            if (p.Opacity <= 0f)
            {
                _splatParticles.RemoveAt(i);
            }
        }
    }

    private void UpdateSanta(float dt)
    {
        if (!Settings.ShowSanta)
        {
            Santa.IsActive = false;
            return;
        }

        if (!Santa.IsActive)
        {
            _santaTimer -= dt;
            if (_santaTimer <= 0f)
            {
                TriggerSantaFlyby();
                _santaTimer = _rng.Next(65, 130);
            }
            return;
        }

        Santa.WavePhase += dt * 3.2f;
        Santa.X += Santa.SpeedX * dt;
        Santa.Y += MathF.Sin(Santa.WavePhase) * 14f * dt;

        for (int i = 0; i < 2; i++)
        {
            Santa.Trail.Add((
                Santa.X - 35f + (float)(_rng.NextDouble() * 14 - 7),
                Santa.Y + (float)(_rng.NextDouble() * 12 - 6),
                1.0f,
                (float)(_rng.NextDouble() * 2.4 + 1.0)
            ));
        }

        for (int i = Santa.Trail.Count - 1; i >= 0; i--)
        {
            var p = Santa.Trail[i];
            float nextAlpha = p.Alpha - dt * 0.85f;
            if (nextAlpha <= 0f)
                Santa.Trail.RemoveAt(i);
            else
                Santa.Trail[i] = (p.X - 15f * dt, p.Y + 8f * dt, nextAlpha, p.Size);
        }

        if (Santa.X > ScreenWidth + 240f)
        {
            Santa.IsActive = false;
            Santa.Trail.Clear();
        }
    }

    private void UpdateThrownSnowballs(float dt)
    {
        for (int i = _snowballs.Count - 1; i >= 0; i--)
        {
            var b = _snowballs[i];
            if (!b.IsThrown) continue;

            b.Vy += 680f * dt;
            b.X += b.Vx * dt;
            b.Y += b.Vy * dt;

            if (b.X <= b.Radius || b.X >= ScreenWidth - b.Radius || b.Y <= b.Radius)
            {
                float hitX = Math.Clamp(b.X, b.Radius, ScreenWidth - b.Radius);
                float hitY = Math.Max(b.Radius, b.Y);
                ShatterSnowball(hitX, hitY, b.Radius, b.Vx, b.Vy);
                _snowballs.RemoveAt(i);
                continue;
            }

            bool hitSurface = false;
            foreach (var surface in _surfaces.Values)
            {
                if (b.X >= surface.Bounds.Left - b.Radius && b.X <= surface.Bounds.Right + b.Radius &&
                    Math.Abs(b.Y - surface.Bounds.Top) <= b.Radius + 10f)
                {
                    int center = Math.Clamp((int)((b.X - surface.Bounds.Left) / WindowSurface.BucketSize), 0, surface.SnowHeights.Length - 1);
                    for (int k = -5; k <= 5; k++)
                    {
                        int idx = center + k;
                        if (idx >= 0 && idx < surface.SnowHeights.Length)
                        {
                            surface.SnowHeights[idx] = Math.Min(WindowSurface.MaxSnowDepth, surface.SnowHeights[idx] + b.Radius * 0.55f);
                        }
                    }
                    ShatterSnowball(b.X, surface.Bounds.Top, b.Radius, b.Vx, b.Vy);
                    hitSurface = true;
                    break;
                }
            }

            if (hitSurface)
            {
                _snowballs.RemoveAt(i);
                continue;
            }

            if (b.Y >= GroundY - b.Radius + 2f)
            {
                int center = Math.Clamp((int)(b.X / WindowSurface.BucketSize), 0, _floorSnow.Length - 1);
                for (int k = -6; k <= 6; k++)
                {
                    int idx = center + k;
                    if (idx >= 0 && idx < _floorSnow.Length)
                    {
                        _floorSnow[idx] = Math.Min(85f, _floorSnow[idx] + b.Radius * 0.35f);
                    }
                }
                ShatterSnowball(b.X, GroundY - 4f, b.Radius, b.Vx, b.Vy);
                _snowballs.RemoveAt(i);
            }
        }
    }

    public bool CanStackOn(Snowball activeBall, out Snowball? targetTopBall, out int stackCount)
    {
        targetTopBall = null;
        stackCount = 0;

        var nearbyBalls = _snowballs
            .Where(b => !b.IsThrown && MathF.Abs(b.X - activeBall.X) < 32f)
            .OrderBy(b => b.Y)
            .ToList();

        stackCount = nearbyBalls.Count;
        if (stackCount == 0) return false;

        targetTopBall = nearbyBalls[0];

        if (stackCount == 1 && targetTopBall.Radius < MinBaseRadius)
            return false;

        return activeBall.Radius <= targetTopBall.Radius - 2.0f;
    }

    private void HandleSnowballBuilder(int mouseX, int mouseY, float mouseVx, float mouseVy, float mouseSpeed, bool isShiftHeld)
    {
        if (isShiftHeld)
        {
            if (_activeSnowball == null)
            {
                var existing = _snowballs
                    .Where(b => !b.IsThrown)
                    .OrderBy(b => b.Y)
                    .FirstOrDefault(b =>
                    {
                        float dx = b.X - mouseX;
                        float dy = b.Y - mouseY;
                        return MathF.Sqrt(dx * dx + dy * dy) <= b.Radius + 14f;
                    });

                if (existing != null)
                {
                    _snowballs.Remove(existing);
                    _activeSnowball = existing;
                    _activeSnowball.IsHeld = true;
                }
                else if (mouseY >= GroundY - 100f)
                {
                    int bucket = Math.Clamp(mouseX / WindowSurface.BucketSize, 0, _floorSnow.Length - 1);
                    float localSnow = GetLocalSnowAverage(bucket, 3);

                    if (localSnow >= 7.0f)
                    {
                        ConsumeSnow(bucket, 3, 4.0f);
                        _activeSnowball = new Snowball
                        {
                            X = mouseX,
                            Y = mouseY,
                            Radius = MinSnowballRadius,
                            IsHeld = true
                        };
                    }
                }
            }

            if (_activeSnowball != null)
            {
                _activeSnowball.X = mouseX;
                _activeSnowball.Y = mouseY;

                if (mouseSpeed > 1.5f && mouseY >= GroundY - 100f && _activeSnowball.Radius < MaxSnowballRadius)
                {
                    int centerBucket = Math.Clamp(mouseX / WindowSurface.BucketSize, 0, _floorSnow.Length - 1);
                    float collected = ConsumeSnow(centerBucket, 3, 0.85f);

                    if (collected > 0)
                    {
                        _activeSnowball.Radius = MathF.Min(MaxSnowballRadius, _activeSnowball.Radius + collected * 0.045f);
                    }
                }
            }
        }
        else if (_activeSnowball != null)
        {
            _activeSnowball.IsHeld = false;
            float throwSpeed = MathF.Sqrt(mouseVx * mouseVx + mouseVy * mouseVy);

            if (throwSpeed > 420f)
            {
                _activeSnowball.IsThrown = true;
                _activeSnowball.Vx = Math.Clamp(mouseVx * 0.75f, -1400f, 1400f);
                float rawVy = mouseVy * 0.75f;
                if (_activeSnowball.Y >= GroundY - _activeSnowball.Radius - 15f && rawVy > -220f)
                {
                    rawVy = -Math.Clamp(throwSpeed * 0.55f, 280f, 750f);
                }
                _activeSnowball.Vy = Math.Clamp(rawVy, -1250f, 600f);
                _snowballs.Add(_activeSnowball);
            }
            else if (CanStackOn(_activeSnowball, out var targetTopBall, out int stackCount) && targetTopBall != null)
            {
                if (stackCount >= 2)
                {
                    var stackBalls = _snowballs
                        .Where(b => !b.IsThrown && MathF.Abs(b.X - targetTopBall.X) < 32f)
                        .OrderByDescending(b => b.Y)
                        .ToList();

                    float baseX = stackBalls[0].X;
                    float scale = Math.Clamp(stackBalls[0].Radius / 20f, 0.75f, 1.15f);

                    foreach (var b in stackBalls) _snowballs.Remove(b);

                    var hats = Enum.GetValues<SnowmanHatType>();
                    var props = Enum.GetValues<SnowmanPropType>();

                    _snowmen.Add(new Snowman
                    {
                        X = baseX,
                        BaseY = GroundY,
                        Scale = scale,
                        Hat = hats[_rng.Next(hats.Length)],
                        Prop = props[_rng.Next(props.Length)]
                    });
                }
                else
                {
                    _activeSnowball.X = targetTopBall.X;
                    _activeSnowball.Y = targetTopBall.Y - targetTopBall.Radius - _activeSnowball.Radius * 0.82f;
                    _snowballs.Add(_activeSnowball);
                }
            }
            else
            {
                float dropX = _activeSnowball.X;
                if (targetTopBall != null && MathF.Abs(dropX - targetTopBall.X) < targetTopBall.Radius + _activeSnowball.Radius)
                {
                    dropX = targetTopBall.X + (dropX >= targetTopBall.X ? 1 : -1) * (targetTopBall.Radius + _activeSnowball.Radius + 6f);
                }

                _activeSnowball.X = Math.Clamp(dropX, 30f, ScreenWidth - 30f);
                _activeSnowball.Y = GroundY - _activeSnowball.Radius + 2f;
                _snowballs.Add(_activeSnowball);
            }

            _activeSnowball = null;
        }
    }

    private float GetLocalSnowAverage(int centerBucket, int radiusBuckets)
    {
        float sum = 0f;
        int count = 0;
        for (int i = -radiusBuckets; i <= radiusBuckets; i++)
        {
            int idx = centerBucket + i;
            if (idx >= 0 && idx < _floorSnow.Length)
            {
                sum += _floorSnow[idx];
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }

    private float ConsumeSnow(int centerBucket, int radiusBuckets, float maxTakePerBucket)
    {
        float totalTaken = 0f;
        for (int i = -radiusBuckets; i <= radiusBuckets; i++)
        {
            int idx = centerBucket + i;
            if (idx >= 0 && idx < _floorSnow.Length && _floorSnow[idx] > 1.0f)
            {
                float take = MathF.Min(_floorSnow[idx], maxTakePerBucket);
                _floorSnow[idx] -= take;
                totalTaken += take;
            }
        }
        return totalTaken;
    }

    private void HandleWindowSnowDisturbance(int mouseX, int mouseY)
    {
        foreach (var surface in _surfaces.Values)
        {
            if (Math.Abs(mouseY - surface.Bounds.Top) < 28 &&
                mouseX >= surface.Bounds.Left && mouseX <= surface.Bounds.Right)
            {
                int centerBucket = (mouseX - surface.Bounds.Left) / WindowSurface.BucketSize;
                for (int offset = -5; offset <= 5; offset++)
                {
                    int idx = centerBucket + offset;
                    if (idx >= 0 && idx < surface.SnowHeights.Length && surface.SnowHeights[idx] > 1f)
                    {
                        surface.SnowHeights[idx] *= 0.80f;
                    }
                }
            }
        }
    }

    private void SmoothFloor()
    {
        for (int i = 1; i < _floorSnow.Length - 1; i++)
        {
            _floorSnow[i] = (_floorSnow[i - 1] + _floorSnow[i] * 2f + _floorSnow[i + 1]) / 4f;
        }
    }

    public void SpawnSnowmanAtRandom()
    {
        float x = _rng.Next(120, Math.Max(200, ScreenWidth - 280));
        var hats = Enum.GetValues<SnowmanHatType>();
        var props = Enum.GetValues<SnowmanPropType>();

        _snowmen.Add(new Snowman
        {
            X = x,
            BaseY = GroundY,
            Scale = 1.0f,
            Hat = hats[_rng.Next(hats.Length)],
            Prop = props[_rng.Next(props.Length)]
        });
    }
}