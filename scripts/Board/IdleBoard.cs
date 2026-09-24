using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// The incremental Plinko board. Balls are consumables taken from IdleManager's stock: each
// drop (click or auto-dropper) spends one, it lands, pays out and is destroyed. To keep
// physics cheap at high cadence, one physical ball can carry a bundle of balls (its Stack)
// and pays for all of them.
public partial class IdleBoard : Node2D, IPlacementBoard
{
    public Rect2 Area = new(0f, 0f, 800f, 616f);

    public event Action<Slot, Ball, double, bool> Landed;   // slot, ball, payout, crit
    public event Action<Chest, string, string> GoldenChestOpened;
    public event Action<Vector2> BallDuplicated;

    private const int FirstRowPegCount = 3;
    private const float MaxSpacing = 58f;
    private const float RowRatio = 0.9f;
    private const float BaseSpacing = 48f;
    private const int MaxBallsInFlight = 170;
    private const double MaxPhysicalDropsPerSecond = 24.0;
    private const double GoldenChestLifetime = 20.0;

    private Node2D _pegs;
    private Node2D _slots;
    private Node2D _walls;
    private Node2D _portals;
    private Node2D _chests;
    private Node2D _balls;
    private readonly List<Slot> _slotList = new();
    private readonly HashSet<Vector2I> _occupied = new();

    private double _autoDropBudget;
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

    private int SlotCount => _rows + FirstRowPegCount;
    private float LauncherY => _top - _s * 1.05f;
    private float LastRowY => _top + (_rows - 1) * _sy;
    private float SlotHeight => _s * 1.25f;
    private float SlotTop => LastRowY + _s * 0.55f;
    private float FloorY => SlotTop + SlotHeight + _s * 0.25f;
    private float WallLeft => _cx - SlotCount * _s / 2f;
    private float WallRight => _cx + SlotCount * _s / 2f;
    // You aim between the outer pegs of the first row, not at the edges.
    private float AimMin => _cx - _s * 0.9f;
    private float AimMax => _cx + _s * 0.9f;

    // Angled rails hug the peg pyramid, 0.62 spacing outside its outermost pegs: just wide
    // enough for a ball to pass, so it can never fall outside the pyramid and slide down a
    // side wall straight into an edge slot. Reaching an edge takes going "outward" at every
    // single row (about 1 in 2^rows).
    private const float RailOffset = 0.62f;
    private float RailHalfWidthAt(float y) => (1f + RailOffset + 0.5f * Mathf.Max(0f, (y - _top) / _sy)) * _s;
    public float AimRangeMin => AimMin;
    public float AimRangeMax => AimMax;
    public Vector2 LauncherPosition => new(_aimX, LauncherY);
    public Vector2 InstructionAnchor => new(_cx, LauncherY - _s * 0.15f);

    // Balls carried by each auto-dropped physical ball.
    public double Bundle => Math.Max(1.0, Math.Ceiling(IdleManager.Instance.Cadence / MaxPhysicalDropsPerSecond));

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
                var (startX, _) = RowRange(row);
                for (int i = 0; i < FirstRowPegCount + row; i++)
                {
                    _pegs.AddChild(new Peg { Position = new Vector2(startX + i * _s, RowY(row)), Radius = 6f * Unit });
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

    private (float startX, float endX) RowRange(int row)
    {
        float width = (FirstRowPegCount + row - 1) * _s;
        float startX = _cx - width / 2f;
        return (startX, startX + width);
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

        // Pyramid rails: one convex block per side filling everything outside the rail.
        float blockTop = Area.Position.Y - 400f;
        float railBottom = RailHalfWidthAt(SlotTop);
        foreach (float side in new[] { -1f, 1f })
        {
            var points = new[]
            {
                new Vector2(_cx + side * RailHalfWidthAt(_top), blockTop),
                new Vector2(_cx + side * RailHalfWidthAt(_top), _top),
                new Vector2(_cx + side * railBottom, SlotTop),
                new Vector2(_cx + side * (SlotCount * _s / 2f + 40f), SlotTop),
                new Vector2(_cx + side * (SlotCount * _s / 2f + 40f), blockTop),
            };
            if (side > 0f) System.Array.Reverse(points);
            var rail = new StaticBody2D
            {
                CollisionLayer = PhysicsLayers.Board,
                CollisionMask = 0,
                PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.25f, Friction = 0.05f },
            };
            rail.AddChild(new CollisionShape2D { Shape = new ConvexPolygonShape2D { Points = points } });
            _walls.AddChild(rail);
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
    }

    private void OnSlotEntered(Slot slot, Ball ball)
    {
        if (ball.IsQueuedForDeletion())
        {
            return;
        }
        slot.Pulse();
        LandingCounts[_slotList.IndexOf(slot)]++;
        var (payout, crit) = IdleManager.Instance.Land(ball.Tier, ball.Stack, slot.Multiplier);
        Landed?.Invoke(slot, ball, payout, crit);
        ball.Settle();
    }

    // ---------------------------------------------------------------- cells & portals

    private List<Vector2I> FreeCells(int minRow, int maxRow)
    {
        var cells = new List<Vector2I>();
        for (int row = Math.Max(1, minRow); row <= Math.Min(maxRow, _rows - 2); row++)
        {
            for (int k = 0; k < FirstRowPegCount + row - 1; k++)
            {
                var cell = new Vector2I(row, k);
                if (!_occupied.Contains(cell)) cells.Add(cell);
            }
        }
        return cells;
    }

    public Vector2 CellCenter(Vector2I cell)
    {
        var (startX, _) = RowRange(cell.X);
        return new Vector2(startX + (cell.Y + 0.5f) * _s, RowY(cell.X) + _sy / 2f);
    }

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

    private bool DropNext(float x, double count)
    {
        if (_inFlight >= MaxBallsInFlight)
        {
            return false;
        }
        var (tier, taken) = IdleManager.Instance.TakeForDrop(count);
        if (tier < 0)
        {
            return false;
        }
        Spawn(tier, taken, false, new Vector2(x, LauncherY + _s * 0.2f), new Vector2((float)GD.RandRange(-12.0, 12.0), 40f));
        _launcherPulse = 1f;
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
        float radius = 9f * Unit * (1f + 0.04f * tier);
        position.X = Mathf.Clamp(position.X, WallLeft + radius + 2f, WallRight - radius - 2f);
        var ball = new Ball
        {
            Radius = radius,
            Tier = tier,
            Stack = stack,
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

    // The ball is spent: nothing goes back to the stock.
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
        if (idle.HasAutoDropper && LauncherActive)
        {
            double bundle = Bundle;
            _autoDropBudget = Math.Min(bundle * 4, _autoDropBudget + delta * idle.Cadence);
            while (_autoDropBudget >= bundle)
            {
                // Auto drops scatter a little around the aimed spot.
                float x = _aimX + (float)GD.RandRange(-0.3, 0.3) * _s;
                if (!DropNext(x, bundle))
                {
                    _autoDropBudget = Math.Min(_autoDropBudget, bundle);
                    break;
                }
                _autoDropBudget -= bundle;
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
        float topHalf = RailHalfWidthAt(_top);
        float bottomHalf = RailHalfWidthAt(SlotTop);
        var outline = new[]
        {
            new Vector2(_cx - topHalf, railTop), new Vector2(_cx + topHalf, railTop),
            new Vector2(_cx + topHalf, _top), new Vector2(_cx + bottomHalf, SlotTop),
            new Vector2(WallRight, SlotTop), new Vector2(WallRight, FloorY),
            new Vector2(WallLeft, FloorY), new Vector2(WallLeft, SlotTop),
            new Vector2(_cx - bottomHalf, SlotTop), new Vector2(_cx - topHalf, _top),
        };
        var fieldTop = new Color(0.07f, 0.03f, 0.1f, 0.88f);
        var fieldBottom = new Color(0.03f, 0.01f, 0.06f, 0.95f);
        var fieldColors = new Color[outline.Length];
        for (int i = 0; i < outline.Length; i++)
        {
            fieldColors[i] = fieldTop.Lerp(fieldBottom, Mathf.Clamp((outline[i].Y - railTop) / (FloorY - railTop), 0f, 1f));
        }
        DrawPolygon(outline, fieldColors);

        var (firstStart, firstEnd) = RowRange(0);
        var (lastStart, lastEnd) = RowRange(_rows - 1);
        var glowTop = new Color(0.55f, 0.2f, 0.7f, 0.16f);
        var glowBottom = new Color(0.3f, 0.1f, 0.5f, 0.05f);
        DrawPolygon(new[]
        {
            new Vector2(firstStart - _s, _top - _s * 0.5f), new Vector2(firstEnd + _s, _top - _s * 0.5f),
            new Vector2(lastEnd + _s, LastRowY + _s * 0.4f), new Vector2(lastStart - _s, LastRowY + _s * 0.4f),
        }, new[] { glowTop, glowTop, glowBottom, glowBottom });

        foreach (float side in new[] { -1f, 1f })
        {
            var rail = new[]
            {
                new Vector2(_cx + side * topHalf, railTop), new Vector2(_cx + side * topHalf, _top),
                new Vector2(_cx + side * bottomHalf, SlotTop), new Vector2(_cx + side * SlotCount * _s / 2f, SlotTop),
                new Vector2(_cx + side * SlotCount * _s / 2f, FloorY),
            };
            DrawPolyline(rail, Pal.Alpha(Pal.Pink, 0.25f), 8f, true);
            DrawPolyline(rail, Pal.Hdr(Pal.Pink, 1.8f), 2.5f, true);
        }

        // Launcher rail + nozzle.
        float y = LauncherY;
        DrawLine(new Vector2(AimMin - _s * 0.3f, y - _s * 0.35f), new Vector2(AimMax + _s * 0.3f, y - _s * 0.35f), Pal.Alpha(Pal.Cyan, 0.35f), 2f);
        if (!LauncherActive)
        {
            return;
        }
        bool ready = IdleManager.Instance.TotalStock >= 1;
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
