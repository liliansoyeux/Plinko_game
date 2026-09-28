using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// The incremental Plinko board. One ball at a time, free and endless, always of the tier
// forged in IdleManager: it drops (click or auto-dropper), lands, pays out and is destroyed,
// then the next one can go. Portal twins are the only extra balls.
public partial class IdleBoard : Node2D, IPlacementBoard
{
    public Rect2 Area = new(0f, 0f, 800f, 616f);

    public event Action<Slot, Ball, double, bool> Landed;   // slot, ball, payout, crit
    public event Action<Chest, string, string> GoldenChestOpened;
    public event Action<Vector2> BallDuplicated;
    public event Action<Vector2> JackpotHit;

    private const float MaxSpacing = 58f;
    private const float RowRatio = 0.9f;
    private const float BaseSpacing = 48f;
    private const int MaxBallsInFlight = 32;     // portal twins included
    private const double RelaunchDelay = 0.35;    // auto-dropper pause between two balls
    private const double GoldenChestLifetime = 20.0;

    private Node2D _pegs;
    private Node2D _slots;
    private Node2D _walls;
    private Node2D _portals;
    private Node2D _chests;
    private Node2D _balls;
    private readonly List<Slot> _slotList = new();
    private readonly HashSet<Vector2I> _occupied = new();

    private double _relaunchTimer;
    private int _jackpotIndex = -1;
    private int _inFlight;

    private int _rows = -1;
    private float _s = BaseSpacing;
    private float _sy = BaseSpacing * RowRatio;
    private float _top;
    private float _cx;
    private float _aimX;
    private float _aimTargetX;
    private float _launcherPulse;
    private float _time;

    private Chest _goldenChest;
    private double _goldenChestTimer;
    private double _goldenChestAge;

    // Debug/balancing: how many balls landed in each slot of the current layout.
    public int[] LandingCounts { get; private set; } = new int[32];

    public float Unit => _s / BaseSpacing;
    public float Spacing => _s;
    public bool LauncherActive { get; set; } = true;

    private static int SlotCount => IdleManager.SlotCount;
    private float LauncherY => _top - _s * 1.05f;
    private float LastRowY => _top + (_rows - 1) * _sy;
    private float SlotHeight => _s * 1.25f;
    private float SlotTop => LastRowY + _s * 0.55f;
    private float FloorY => SlotTop + SlotHeight + _s * 0.25f;
    private float WallLeft => _cx - SlotCount * _s / 2f;
    private float WallRight => _cx + SlotCount * _s / 2f;
    // Rectangular board (like the TV-show Plinko): staggered rows span the full width, so
    // a ball always meets pegs and can't slide down a wall. The launcher can aim across
    // the width of the first peg row (manual drops); auto drops spawn at random there too.
    // Launch zone (click and auto drops): from the first to the last peg of the first row,
    // so a ball dropped at the far side can't just fall straight into an edge slot.
    private float AimMin => PegX(0, 0);
    private float AimMax => PegX(0, PegCount(0) - 1);

    // Rows alternate between two peg layouts; the last row always has its pegs on the slot
    // dividers so balls fall cleanly into the slots.
    private bool IsDividerRow(int row) => (_rows - 1 - row) % 2 == 0;
    private int PegCount(int row) => IsDividerRow(row) ? SlotCount - 1 : SlotCount;
    // Every peg is aligned either on a slot divider or on a slot centre.
    private float PegX(int row, int k) => WallLeft + (IsDividerRow(row) ? k + 1f : k + 0.5f) * _s;
    public float AimRangeMin => AimMin;
    public float AimRangeMax => AimMax;
    public Vector2 LauncherPosition => new(_aimX, LauncherY);
    public Vector2 InstructionAnchor => new(_cx, LauncherY - _s * 0.15f);

    public override void _Ready()
    {
        _walls = new Node2D();
        _slots = new Node2D();
        _pegs = new Node2D();
        _portals = new Node2D();
        _chests = new Node2D();
        _balls = new Node2D();
        AddChild(_walls);
        AddChild(_slots);
        AddChild(_pegs);
        AddChild(_portals);
        AddChild(_chests);
        AddChild(_balls);

        IdleManager.Instance.BoardLayoutChanged += Rebuild;
        _cx = Area.Position.X + Area.Size.X / 2f;
        _aimX = _aimTargetX = _cx;
        _goldenChestTimer = 30.0;
        Rebuild();
    }

    public override void _ExitTree()
    {
        IdleManager.Instance.BoardLayoutChanged -= Rebuild;
    }

    // ---------------------------------------------------------------- layout

    public void Rebuild()
    {
        var idle = IdleManager.Instance;
        bool rowsChanged = idle.Rows != _rows;
        _rows = idle.Rows;

        float byWidth = Area.Size.X / (SlotCount + 0.2f);
        float byHeight = Area.Size.Y / ((_rows - 1) * RowRatio + 3.4f);
        _s = Mathf.Min(MaxSpacing, Mathf.Min(byWidth, byHeight));
        _sy = _s * RowRatio;
        _cx = Area.Position.X + Area.Size.X / 2f;
        float used = (_rows - 1) * _sy + 3.4f * _s;
        _top = Area.Position.Y + (Area.Size.Y - used) / 2f + _s * 1.35f;

        if (rowsChanged)
        {
            Clear(_pegs);
            Clear(_walls);
            for (int row = 0; row < _rows; row++)
            {
                for (int i = 0; i < PegCount(row); i++)
                {
                    _pegs.AddChild(new Peg { Position = new Vector2(PegX(row, i), RowY(row)), Radius = 6f * Unit });
                }
            }
            CreateWalls();
            RebuildPortals();
            if (_goldenChest != null)
            {
                _goldenChest.QueueFree();
                _goldenChest = null;
            }
        }

        BuildSlots();
        _aimTargetX = Mathf.Clamp(_aimTargetX, AimMin, AimMax);
    }

    private float RowY(int row) => _top + row * _sy;

    private void CreateWalls()
    {
        const float thickness = 40f;
        float top = Area.Position.Y - 400f;
        float height = FloorY + 20f - top;
        foreach (float x in new[] { WallLeft - thickness / 2f, WallRight + thickness / 2f })
        {
            var wall = new StaticBody2D
            {
                Position = new Vector2(x, top + height / 2f),
                CollisionLayer = PhysicsLayers.Board,
                CollisionMask = 0,
                PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.3f, Friction = 0.1f },
            };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(thickness, height) } });
            _walls.AddChild(wall);
        }

        for (int i = 0; i <= SlotCount; i++)
        {
            var post = new StaticBody2D { Position = new Vector2(WallLeft + i * _s, SlotTop - 1f), CollisionLayer = PhysicsLayers.Board, CollisionMask = 0 };
            post.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 2.5f * Unit } });
            _walls.AddChild(post);
        }

        var floor = new Area2D { Position = new Vector2(_cx, FloorY), CollisionLayer = 0, CollisionMask = PhysicsLayers.Ball, Monitorable = false };
        floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(WallRight - WallLeft + 40f, 30f) } });
        floor.BodyEntered += body => (body as Ball)?.Settle();
        _walls.AddChild(floor);
    }

    private void BuildSlots()
    {
        Clear(_slots);
        _slotList.Clear();
        var values = IdleManager.Instance.SlotMultipliers();
        for (int i = 0; i < SlotCount; i++)
        {
            var slot = new Slot
            {
                Position = new Vector2(WallLeft + (i + 0.5f) * _s, SlotTop + SlotHeight / 2f),
                Multiplier = (float)values[i],
                SlotSize = new Vector2(_s, SlotHeight),
            };
            slot.BallEntered += OnSlotEntered;
            _slots.AddChild(slot);
            _slotList.Add(slot);
        }
        _jackpotIndex = -1;
        UpdateJackpot();
    }

    // Case jackpot: one slot glows and pays extra, then the jackpot moves elsewhere.
    private void UpdateJackpot(bool move = false)
    {
        var idle = IdleManager.Instance;
        if (!idle.HasJackpot || _slotList.Count == 0)
        {
            return;
        }
        if (move || _jackpotIndex < 0)
        {
            int next;
            do { next = (int)(GD.Randi() % (uint)_slotList.Count); } while (next == _jackpotIndex && _slotList.Count > 1);
            _jackpotIndex = next;
        }
        for (int i = 0; i < _slotList.Count; i++)
        {
            _slotList[i].Jackpot = i == _jackpotIndex;
            _slotList[i].JackpotMultiplier = (float)idle.JackpotMultiplier;
        }
    }

    private void OnSlotEntered(Slot slot, Ball ball)
    {
        if (ball.IsQueuedForDeletion())
        {
            return;
        }
        slot.Pulse();
        int index = _slotList.IndexOf(slot);
        LandingCounts[index]++;
        bool jackpot = slot.Jackpot;
        double multiplier = slot.Multiplier * (jackpot ? slot.JackpotMultiplier : 1f);
        var (payout, crit) = IdleManager.Instance.Land(ball.Tier, ball.Stack, multiplier, ball.PegHits);
        Landed?.Invoke(slot, ball, payout, crit);
        ball.Settle();
        if (jackpot)
        {
            JackpotHit?.Invoke(slot.GlobalPosition);
            UpdateJackpot(move: true);
        }
    }

    // ---------------------------------------------------------------- cells & portals

    private List<Vector2I> FreeCells(int minRow, int maxRow)
    {
        var cells = new List<Vector2I>();
        for (int row = Math.Max(1, minRow); row <= Math.Min(maxRow, _rows - 2); row++)
        {
            for (int k = 0; k < PegCount(row) - 1; k++)
            {
                var cell = new Vector2I(row, k);
                if (!_occupied.Contains(cell)) cells.Add(cell);
            }
        }
        return cells;
    }

    public Vector2 CellCenter(Vector2I cell) =>
        new((PegX(cell.X, cell.Y) + PegX(cell.X, cell.Y + 1)) / 2f, RowY(cell.X) + _sy / 2f);

    public List<Vector2I> FreeCellsFor(PlaceableKind kind) => FreeCells(1, _rows - 2);

    public void CommitPlacement(PlaceableKind kind, Vector2I cell, float rotation)
    {
        IdleManager.Instance.CommitPortal(cell);
        _occupied.Add(cell);
        AddPortal(cell, IdleManager.Instance.PortalCells.Count - 1);
    }

    private void RebuildPortals()
    {
        Clear(_portals);
        _occupied.Clear();
        var cells = IdleManager.Instance.PortalCells;
        for (int i = 0; i < cells.Count; i++)
        {
            _occupied.Add(cells[i]);
            AddPortal(cells[i], i);
        }
    }

    private void AddPortal(Vector2I cell, int id)
    {
        var portal = new Portal { Position = CellCenter(cell), Radius = _s * 0.42f, PortalId = id };
        portal.BallEntered += OnPortalEntered;
        _portals.AddChild(portal);
    }

    private void OnPortalEntered(Portal portal, Ball ball)
    {
        if (ball.IsQueuedForDeletion() || !ball.PortalsUsed.Add(portal.PortalId) || _inFlight >= MaxBallsInFlight)
        {
            return;
        }
        portal.Flash();
        var origin = ball.Position;
        var velocity = ball.LinearVelocity;
        var used = new HashSet<int>(ball.PortalsUsed);
        float side = Mathf.Max(60f, Mathf.Abs(velocity.X));
        ball.LinearVelocity = new Vector2(-side, velocity.Y);
        int tier = ball.Tier;
        double stack = ball.Stack;
        float radius = ball.Radius;

        // Spawned deferred: we're inside a physics callback.
        Callable.From(() =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            var twin = Spawn(tier, stack, true, origin + new Vector2(radius * 0.6f, 0f), new Vector2(side, velocity.Y));
            twin.PortalsUsed.UnionWith(used);
            BallDuplicated?.Invoke(ToGlobal(origin));
        }).CallDeferred();
    }

    // ---------------------------------------------------------------- dropping

    // Only one ball on the board at a time.
    public bool BallInPlay => _inFlight > 0;

    private bool DropNext(float x, double count, bool launcher = true)
    {
        if (BallInPlay)
        {
            return false;
        }
        // Balls are free and endless: always the currently forged tier.
        double taken = Math.Max(1, Math.Floor(count));
        int tier = IdleManager.Instance.BallTier;
        IdleManager.Instance.CountDrop(taken);
        // Auto drops appear just above the first row, a little randomly in height too.
        float y = launcher ? LauncherY + _s * 0.2f : _top - _s * (0.5f + (float)GD.Randf() * 0.4f);
        Spawn(tier, taken, false, new Vector2(x, y), new Vector2((float)GD.RandRange(-12.0, 12.0), 40f));
        // Bille jumelle: sometimes a second ball goes along.
        if (GD.Randf() < IdleManager.Instance.TwinChance)
        {
            float side = x < _cx ? 1f : -1f;
            Spawn(tier, taken, true, new Vector2(x + side * _s * 0.8f, y), new Vector2(side * 30f, 40f));
            IdleManager.Instance.CountDrop(taken);
        }
        if (launcher)
        {
            _launcherPulse = 1f;
        }
        return true;
    }

    public bool ManualDrop()
    {
        bool dropped = DropNext(_aimX + (float)GD.RandRange(-0.06, 0.06) * _s, 1);
        if (dropped)
        {
            Sfx.Play(Sound.Drop, 0.9f + GD.Randf() * 0.2f, -6f);
        }
        return dropped;
    }

    private Ball Spawn(int tier, double stack, bool twin, Vector2 position, Vector2 velocity)
    {
        // Small enough (max ~15.4 across at tier 5) to fit the 18-wide gap between a wall and
        // the outermost peg of a row.
        float radius = 7f * Unit * (1f + 0.02f * tier);
        position.X = Mathf.Clamp(position.X, WallLeft + radius + 2f, WallRight - radius - 2f);
        var ball = new Ball
        {
            Radius = radius,
            Tier = tier,
            Stack = stack,
            GravityScale = (float)IdleManager.Instance.BallSpeed,
            IsTwin = twin,
            Position = position,
            LinearVelocity = velocity,
        };
        ball.PegHit += OnBallPegHit;
        ball.Removed += OnBallRemoved;
        _balls.AddChild(ball);
        _inFlight++;
        return ball;
    }

    private void OnBallPegHit(Ball ball) => IdleManager.Instance.PegHit(ball.Tier, ball.Stack);

    private void OnBallRemoved(Ball ball) => _inFlight = Math.Max(0, _inFlight - 1);

    public void AimAtGlobal(Vector2 global) => _aimTargetX = Mathf.Clamp(ToLocal(global).X, AimMin, AimMax);
    public void AimAtLocalX(float x) => _aimTargetX = Mathf.Clamp(x, AimMin, AimMax);

    // ---------------------------------------------------------------- golden chest

    private void UpdateGoldenChest(double delta)
    {
        if (_goldenChest != null)
        {
            _goldenChestAge += delta;
            if (_goldenChestAge > GoldenChestLifetime)
            {
                _occupied.Remove(_goldenChest.Cell);
                _goldenChest.QueueFree();
                _goldenChest = null;
                _goldenChestTimer = IdleManager.Instance.GoldenChestInterval;
            }
            return;
        }

        _goldenChestTimer -= delta;
        if (_goldenChestTimer > 0)
        {
            return;
        }
        var cells = FreeCells(2, _rows - 2);
        if (cells.Count == 0)
        {
            _goldenChestTimer = 10;
            return;
        }
        var cell = cells[(int)(GD.Randi() % (uint)cells.Count)];
        _occupied.Add(cell);
        _goldenChestAge = 0;
        _goldenChest = new Chest { Rarity = Rarity.Epic, Cell = cell, Unit = Unit, Position = CellCenter(cell) };
        _goldenChest.Opened += OnGoldenChestOpened;
        _chests.AddChild(_goldenChest);
        Sfx.Play(Sound.Chest, 1.3f, -4f);
        GD.Print($"[Board] golden chest at {cell}");
    }

    private void OnGoldenChestOpened(Chest chest)
    {
        _occupied.Remove(chest.Cell);
        var (title, detail) = IdleManager.Instance.OpenGoldenChest();
        GD.Print($"[Board] golden chest opened: {title} {detail}");
        GoldenChestOpened?.Invoke(chest, title, detail);
        chest.QueueFree();
        _goldenChest = null;
        _goldenChestTimer = IdleManager.Instance.GoldenChestInterval;
    }

    // ---------------------------------------------------------------- loop & drawing

    public override void _Process(double delta)
    {
        _time += (float)delta;

        var idle = IdleManager.Instance;
        if (idle.HasAutoDropper && idle.AutoDropEnabled && LauncherActive && !BallInPlay)
        {
            _relaunchTimer += delta * idle.BallSpeed;
            if (_relaunchTimer >= RelaunchDelay)
            {
                // Auto drops appear at a random spot above the field, not from the launcher.
                _relaunchTimer = 0;
                DropNext((float)GD.RandRange(AimMin, AimMax), 1, launcher: false);
            }
        }

        UpdateGoldenChest(delta);
        _aimX = Mathf.Lerp(_aimX, _aimTargetX, 1f - Mathf.Exp(-(float)delta * 18f));
        _launcherPulse = Mathf.Max(0f, _launcherPulse - (float)delta * 4f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float railTop = LauncherY - _s * 0.5f;
        var field = new Rect2(WallLeft, railTop, WallRight - WallLeft, FloorY - railTop);
        Paint.VerticalGradient(this, field, new Color(0.07f, 0.03f, 0.1f, 0.88f), new Color(0.03f, 0.01f, 0.06f, 0.95f));
        Paint.VerticalGradient(this, new Rect2(WallLeft, _top - _s * 0.5f, WallRight - WallLeft, LastRowY - _top + _s),
            new Color(0.55f, 0.2f, 0.7f, 0.12f), new Color(0.3f, 0.1f, 0.5f, 0.04f));

        foreach (float x in new[] { WallLeft, WallRight })
        {
            DrawLine(new Vector2(x, railTop), new Vector2(x, FloorY), Pal.Alpha(Pal.Pink, 0.25f), 8f);
            DrawLine(new Vector2(x, railTop), new Vector2(x, FloorY), Pal.Hdr(Pal.Pink, 1.8f), 2.5f);
        }

        // Launcher rail + nozzle.
        float y = LauncherY;
        DrawLine(new Vector2(AimMin - _s * 0.3f, y - _s * 0.35f), new Vector2(AimMax + _s * 0.3f, y - _s * 0.35f), Pal.Alpha(Pal.Cyan, 0.35f), 2f);
        if (!LauncherActive)
        {
            return;
        }
        bool ready = !BallInPlay;
        var color = ready ? Pal.Cyan : new Color(0.4f, 0.38f, 0.45f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 5f);
        if (ready)
        {
            DrawDashedLine(new Vector2(_aimX, y + _s * 0.3f), new Vector2(_aimX, _top - _s * 0.3f),
                Pal.Alpha(Pal.Hdr(Pal.Cyan, 1.3f), 0.35f + 0.2f * pulse), 1.5f, 5f);
        }
        float squash = 1f + 0.35f * _launcherPulse;
        var nozzle = new[]
        {
            new Vector2(_aimX - _s * 0.42f * squash, y - _s * 0.5f), new Vector2(_aimX + _s * 0.42f * squash, y - _s * 0.5f),
            new Vector2(_aimX + _s * 0.2f, y - _s * 0.05f), new Vector2(_aimX - _s * 0.2f, y - _s * 0.05f),
        };
        Paint.Halo(this, new Vector2(_aimX, y - _s * 0.25f), _s * 0.9f, Pal.Alpha(color, 0.25f + 0.3f * _launcherPulse));
        DrawColoredPolygon(nozzle, color.Darkened(0.55f));
        DrawPolyline(Paint.Closed(nozzle), Pal.Hdr(color, 1.6f + _launcherPulse), 2f, true);
    }

    private static void Clear(Node container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}
