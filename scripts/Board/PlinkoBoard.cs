using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

public partial class PlinkoBoard : Node2D
{
    // Local-space rectangle the whole board (launcher to floor) must fit in. Peg spacing is
    // derived from it, so adding rows shrinks the board instead of overflowing the machine.
    public Rect2 Area = new(0f, 0f, 800f, 616f);

    private const int FirstRowPegCount = 3;
    private const float MaxSpacing = 58f;
    private const float RowRatio = 0.9f;
    private const float BaseSpacing = 48f;


    private static readonly float[] GoodReplacementValues = { 3f, 5f, 10f, 41f };

    public event Action<Chest> ChestOpened;
    public event Action<Chest> ChestSpawned;
    public event Action<PlaceableKind> PlacementRequested;
    public event Action<Vector2> BallDuplicated;
    public event Action<Slot, Ball> SlotEntered;

    private Node2D _pegsContainer;
    private Node2D _slotsContainer;
    private Node2D _ballsContainer;
    private Node2D _wallsContainer;
    private Node2D _chestsContainer;
    private Node2D _blockersContainer;
    private Node2D _portalsContainer;

    // Things the player positioned themselves (bonus blockers, portals). They keep their
    // cell across paliers and board rebuilds, unlike the random "wild" blockers.
    private readonly List<(PlaceableKind kind, Vector2I cell, float rotation)> _placements = new();

    private readonly List<Slot> _slots = new();
    private readonly List<Chest> _chests = new();
    private readonly HashSet<Vector2I> _occupiedCells = new();

    // Persisted slot values so they survive palier transitions and boss-modifier rebuilds —
    // only reshuffled when a new run starts (a new board), or extended when a row is added.
    private List<float> _slotMultipliers;

    private RunStats _effective = new();
    private bool _generated;
    private int _builtRows = -1;
    private int _builtBlockers;
    private float _builtSlotWidth = -1f;

    private float _s = BaseSpacing;
    private float _sy = BaseSpacing * RowRatio;
    private float _top;
    private float _cx;
    private float _aimX;
    private float _aimTargetX;
    private float _launcherPulse;
    private float _time;

    public float Unit => _s / BaseSpacing;
    public bool LauncherActive { get; set; } = true;

    private int SlotCount => _effective.RowCount + FirstRowPegCount;
    private float LauncherY => _top - _s * 1.05f;
    private float LastRowY => _top + (_effective.RowCount - 1) * _sy;
    private float SlotHeight => _s * 1.25f;
    private float SlotTop => LastRowY + _s * 0.55f;
    private float FloorY => SlotTop + SlotHeight + _s * 0.25f;
    private float WallLeft => _cx - SlotCount * _s / 2f;
    private float WallRight => _cx + SlotCount * _s / 2f;
    public Vector2 LauncherPosition => new(_aimX, LauncherY);

    public override void _Ready()
    {
        _pegsContainer = new Node2D { Name = "Pegs" };
        _slotsContainer = new Node2D { Name = "Slots" };
        _wallsContainer = new Node2D { Name = "Walls" };
        _blockersContainer = new Node2D { Name = "Blockers" };
        _portalsContainer = new Node2D { Name = "Portals" };
        _chestsContainer = new Node2D { Name = "Chests" };
        _ballsContainer = new Node2D { Name = "Balls" };
        AddChild(_wallsContainer);
        AddChild(_slotsContainer);
        AddChild(_pegsContainer);
        AddChild(_portalsContainer);
        AddChild(_blockersContainer);
        AddChild(_chestsContainer);
        AddChild(_ballsContainer);

        var run = RunManager.Instance;
        run.ChestSpawnRequested += OnChestSpawnRequested;
        run.MalusChestSpawnRequested += OnMalusChestSpawnRequested;
        run.StatsChanged += OnStatsChanged;
        run.PalierStarted += OnPalierStarted;
        run.BoardActionRequested += OnBoardActionRequested;

        _cx = Area.Position.X + Area.Size.X / 2f;
        _aimX = _aimTargetX = _cx;
    }

    public override void _ExitTree()
    {
        var run = RunManager.Instance;
        run.ChestSpawnRequested -= OnChestSpawnRequested;
        run.MalusChestSpawnRequested -= OnMalusChestSpawnRequested;
        run.StatsChanged -= OnStatsChanged;
        run.PalierStarted -= OnPalierStarted;
        run.BoardActionRequested -= OnBoardActionRequested;
    }

    // ---------------------------------------------------------------- generation

    private void Generate()
    {
        _effective = BuildEffectiveStats();
        ComputeLayout();

        ClearChildren(_pegsContainer);
        ClearChildren(_wallsContainer);
        ClearChildren(_blockersContainer);
        ClearChildren(_portalsContainer);

        GeneratePegs();
        GenerateSlots();
        CreateWalls();

        _occupiedCells.Clear();
        RebuildPlacements();
        foreach (var chest in _chests)
        {
            PlaceChest(chest, keepCell: true);
        }

        _builtBlockers = 0;
        for (int i = 0; i < _effective.BlockerCount; i++)
        {
            AddBlocker(0f);
        }
        _builtBlockers = RunManager.Instance.Stats.BlockerCount;

        var palier = RunManager.Instance.CurrentPalier;
        if (palier != null)
        {
            foreach (var modifier in palier.Modifiers)
            {
                if (modifier.Kind == BossModifierKind.SpinningBlockers)
                {
                    for (int i = 0; i < (int)modifier.Value; i++)
                    {
                        AddBlocker(i % 2 == 0 ? 1.7f : -1.7f);
                    }
                }
            }
        }

        _builtRows = _effective.RowCount;
        _builtSlotWidth = _effective.SlotWidthModifier;
        _generated = true;
        _aimTargetX = Mathf.Clamp(_aimTargetX, AimMin, AimMax);
        QueueRedraw();
    }

    // Board-generation-only stats: the permanent RunStats plus the current palier's boss
    // modifiers layered on top of a clone. Never mutates RunManager.Instance.Stats, so a
    // boss's extra hardship disappears once that palier ends.
    private static RunStats BuildEffectiveStats()
    {
        var stats = RunManager.Instance.Stats.Clone();
        var palier = RunManager.Instance.CurrentPalier;
        if (palier == null)
        {
            return stats;
        }

        foreach (var modifier in palier.Modifiers)
        {
            switch (modifier.Kind)
            {
                case BossModifierKind.ExtraBlockers:
                    stats.BlockerCount += (int)modifier.Value;
                    break;
                case BossModifierKind.NarrowerSlots:
                    stats.SlotWidthModifier *= modifier.Value;
                    break;
            }
        }
        return stats;
    }

    private void ComputeLayout()
    {
        int rows = _effective.RowCount;
        float byWidth = Area.Size.X / (SlotCount + 0.2f);
        float byHeight = Area.Size.Y / ((rows - 1) * RowRatio + 3.4f);
        _s = Mathf.Min(MaxSpacing, Mathf.Min(byWidth, byHeight));
        _sy = _s * RowRatio;
        _cx = Area.Position.X + Area.Size.X / 2f;

        float used = (rows - 1) * _sy + 3.4f * _s;
        _top = Area.Position.Y + (Area.Size.Y - used) / 2f + _s * 1.35f;
    }

    private (float startX, float endX) GetRowRange(int rowIndex)
    {
        int pegCount = FirstRowPegCount + rowIndex;
        float rowWidth = (pegCount - 1) * _s;
        float startX = _cx - rowWidth / 2f;
        return (startX, startX + rowWidth);
    }

    private float RowY(int row) => _top + row * _sy;

    private void GeneratePegs()
    {
        for (int row = 0; row < _effective.RowCount; row++)
        {
            var (startX, _) = GetRowRange(row);
            int pegCount = FirstRowPegCount + row;
            for (int i = 0; i < pegCount; i++)
            {
                _pegsContainer.AddChild(new Peg
                {
                    Position = new Vector2(startX + i * _s, RowY(row)),
                    Radius = 6f * Unit,
                });
            }
        }
    }

    private void CreateWalls()
    {
        const float thickness = 40f;
        float top = Area.Position.Y - 400f;
        float height = FloorY + 20f - top;
        CreateWall(new Vector2(WallLeft - thickness / 2f, top + height / 2f), new Vector2(thickness, height));
        CreateWall(new Vector2(WallRight + thickness / 2f, top + height / 2f), new Vector2(thickness, height));

        // Slot divider posts: tiny rounded tops so balls resting on them roll off.
        for (int i = 0; i <= SlotCount; i++)
        {
            var post = new StaticBody2D
            {
                Position = new Vector2(WallLeft + i * _s, SlotTop - 1f),
                CollisionLayer = PhysicsLayers.Board,
                CollisionMask = 0,
            };
            post.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 2.5f * Unit } });
            _wallsContainer.AddChild(post);
        }

        var floor = new Area2D
        {
            Position = new Vector2(_cx, FloorY),
            CollisionLayer = 0,
            CollisionMask = PhysicsLayers.Ball,
            Monitorable = false,
        };
        floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(WallRight - WallLeft + 40f, 30f) } });
        floor.BodyEntered += OnFloorCatch;
        _wallsContainer.AddChild(floor);
    }

    private void CreateWall(Vector2 position, Vector2 size)
    {
        var wall = new StaticBody2D
        {
            Position = position,
            CollisionLayer = PhysicsLayers.Board,
            CollisionMask = 0,
            PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.3f, Friction = 0.1f },
        };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
        _wallsContainer.AddChild(wall);
    }

    private void OnFloorCatch(Node2D body)
    {
        if (body is Ball ball && !ball.IsQueuedForDeletion())
        {
            RunManager.Instance.RegisterMiss(ball);
            ball.Settle();
        }
    }

    private void EnsureSlotMultipliers(int slotCount)
    {
        var rng = new Random();
        if (_slotMultipliers == null)
        {
            _slotMultipliers = SlotLayout.Generate(slotCount, _effective.RowCount, rng);
            GD.Print($"[Board] layout {string.Join(" ", _slotMultipliers)} ev0={SlotLayout.ExpectedValue(_slotMultipliers, _effective.RowCount, 0f):0.0}");
            return;
        }

        // Grow alternately on the right and left so the existing layout stays centred
        // under the launcher instead of drifting half a slot per added row.
        while (_slotMultipliers.Count < slotCount)
        {
            float value = SlotLayout.ExtensionValue(_slotMultipliers, rng);
            if (_slotMultipliers.Count % 2 == 0)
            {
                _slotMultipliers.Insert(0, value);
            }
            else
            {
                _slotMultipliers.Add(value);
            }
        }
    }

    private void GenerateSlots()
    {
        ClearChildren(_slotsContainer);
        _slots.Clear();
        EnsureSlotMultipliers(SlotCount);

        bool halveJackpots = RunManager.Instance.CurrentPalier?.Has(BossModifierKind.ShrunkenJackpots) ?? false;

        for (int i = 0; i < SlotCount; i++)
        {
            float multiplier = _slotMultipliers[i];
            if (halveJackpots && multiplier >= 10f)
            {
                multiplier = Mathf.Round(multiplier * 0.5f);
            }
            var slot = new Slot
            {
                Position = new Vector2(WallLeft + (i + 0.5f) * _s, SlotTop + SlotHeight / 2f),
                Multiplier = multiplier,
                SlotSize = new Vector2(_s * _effective.SlotWidthModifier, SlotHeight),
            };
            slot.BallEntered += OnSlotBallEntered;
            _slotsContainer.AddChild(slot);
            _slots.Add(slot);
        }
    }

    private void OnSlotBallEntered(Slot slot, Ball ball)
    {
        if (ball.IsQueuedForDeletion())
        {
            return;
        }
        slot.Pulse();
        SlotEntered?.Invoke(slot, ball);
        RunManager.Instance.ScoreSlot(ball, slot.Multiplier);
        ball.Settle();
    }

    // ---------------------------------------------------------------- cells, blockers, chests

    private List<Vector2I> FreeCells(int minRow, int maxRow)
    {
        var cells = new List<Vector2I>();
        for (int row = Math.Max(1, minRow); row <= Math.Min(maxRow, _effective.RowCount - 2); row++)
        {
            int gaps = FirstRowPegCount + row - 1;
            for (int k = 0; k < gaps; k++)
            {
                var cell = new Vector2I(row, k);
                if (!_occupiedCells.Contains(cell))
                {
                    cells.Add(cell);
                }
            }
        }
        return cells;
    }

    private Vector2 CellPosition(Vector2I cell)
    {
        var (startX, _) = GetRowRange(cell.X);
        return new Vector2(startX + (cell.Y + 0.5f) * _s, RowY(cell.X) + _sy / 2f);
    }

    private Vector2I? ClaimRandomCell(int minRow, int maxRow)
    {
        var cells = FreeCells(minRow, maxRow);
        if (cells.Count == 0)
        {
            cells = FreeCells(1, _effective.RowCount);
        }
        if (cells.Count == 0)
        {
            return null;
        }
        var cell = cells[(int)(GD.Randi() % (uint)cells.Count)];
        _occupiedCells.Add(cell);
        return cell;
    }

    private void AddBlocker(float spin)
    {
        var cell = ClaimRandomCell(1, _effective.RowCount - 3);
        if (cell == null)
        {
            return;
        }
        _blockersContainer.AddChild(new Blocker
        {
            Position = CellPosition(cell.Value),
            Rotation = Mathf.DegToRad((float)GD.RandRange(-28.0, 28.0)),
            Size = new Vector2(_s * 0.85f, 8f * Unit),
            SpinSpeed = spin,
        });
    }

    // ---------------------------------------------------------------- player placements

    public float Spacing => _s;
    public Vector2 CellCenter(Vector2I cell) => CellPosition(cell);
    public Vector2 InstructionAnchor => new(_cx, LauncherY - _s * 0.15f);

    private int MaxRowFor(PlaceableKind kind) => kind == PlaceableKind.Blocker ? _effective.RowCount - 3 : _effective.RowCount - 2;

    public List<Vector2I> FreeCellsFor(PlaceableKind kind) => FreeCells(1, MaxRowFor(kind));

    private bool IsCellValid(Vector2I cell, PlaceableKind kind) =>
        cell.X >= 1 && cell.X <= MaxRowFor(kind) && cell.Y >= 0 && cell.Y < FirstRowPegCount + cell.X - 1;

    public void CommitPlacement(PlaceableKind kind, Vector2I cell, float rotation)
    {
        _placements.Add((kind, cell, rotation));
        _occupiedCells.Add(cell);
        Instantiate(kind, cell, rotation, _placements.Count - 1);
        GD.Print($"[Board] placed {kind} at {cell}");
    }

    private void RebuildPlacements()
    {
        for (int i = 0; i < _placements.Count; i++)
        {
            var (kind, cell, rotation) = _placements[i];
            if (!IsCellValid(cell, kind) || _occupiedCells.Contains(cell))
            {
                var fallback = ClaimRandomCell(1, MaxRowFor(kind));
                if (fallback == null)
                {
                    continue;
                }
                cell = fallback.Value;
                _placements[i] = (kind, cell, rotation);
            }
            else
            {
                _occupiedCells.Add(cell);
            }
            Instantiate(kind, cell, rotation, i);
        }
    }

    private void Instantiate(PlaceableKind kind, Vector2I cell, float rotation, int id)
    {
        if (kind == PlaceableKind.Blocker)
        {
            _blockersContainer.AddChild(new Blocker
            {
                Position = CellPosition(cell),
                Rotation = rotation,
                Size = new Vector2(_s * 0.85f, 8f * Unit),
                Placed = true,
            });
            return;
        }

        var portal = new Portal { Position = CellPosition(cell), Radius = _s * 0.42f, PortalId = id };
        portal.BallEntered += OnPortalBallEntered;
        _portalsContainer.AddChild(portal);
    }

    private void OnPortalBallEntered(Portal portal, Ball ball)
    {
        if (ball.IsQueuedForDeletion() || !RunManager.Instance.IsPlaying || !ball.PortalsUsed.Add(portal.PortalId))
        {
            return;
        }

        // Register the twin right away (so the palier can't end in between), but spawn it
        // deferred: we're inside a physics callback here.
        RunManager.Instance.RegisterFreeBall();
        portal.Flash();
        var origin = ball.Position;
        var velocity = ball.LinearVelocity;
        var used = new HashSet<int>(ball.PortalsUsed);
        bool golden = ball.IsGolden;
        float radius = ball.Radius;
        int runId = ball.RunId;
        float sideSpeed = Mathf.Max(60f, Mathf.Abs(velocity.X));
        ball.LinearVelocity = new Vector2(-sideSpeed, velocity.Y);

        Callable.From(() =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree())
            {
                return;
            }
            var twin = new Ball
            {
                Radius = radius,
                Position = origin + new Vector2(radius * 0.6f, 0f),
                RunId = runId,
                IsGolden = golden,
                LinearVelocity = new Vector2(sideSpeed, velocity.Y),
            };
            twin.PortalsUsed.UnionWith(used);
            _ballsContainer.AddChild(twin);
            BallDuplicated?.Invoke(ToGlobal(origin));
        }).CallDeferred();
    }

    private void PlaceChest(Chest chest, bool keepCell)
    {
        bool cellValid = keepCell
            && chest.Cell.X >= 2 && chest.Cell.X <= _effective.RowCount - 2
            && chest.Cell.Y < FirstRowPegCount + chest.Cell.X - 1
            && !_occupiedCells.Contains(chest.Cell);

        if (cellValid)
        {
            _occupiedCells.Add(chest.Cell);
        }
        else
        {
            var cell = ClaimRandomCell(2, _effective.RowCount - 2);
            chest.Cell = cell ?? new Vector2I(2, 0);
        }
        chest.Unit = Unit;
        chest.Position = CellPosition(chest.Cell);
    }

    private static Rarity RollChestRarity(float luck)
    {
        float roll = GD.Randf();
        float legendary = 0.02f + luck * 0.04f;
        if (roll < legendary) return Rarity.Legendary;
        roll -= legendary;
        float epic = 0.07f + luck * 0.5f;
        float rare = 0.26f + luck;
        if (roll < epic) return Rarity.Epic;
        if (roll < epic + rare) return Rarity.Rare;
        return Rarity.Common;
    }

    // Level-ups and maluses fire from inside physics callbacks (a ball entering a slot), where
    // adding a monitoring Area2D is forbidden — so chests are spawned deferred.
    private void OnChestSpawnRequested()
    {
        var rarity = RollChestRarity(RunManager.Instance.Stats.ChestLuck);
        Callable.From(() => SpawnChest(rarity)).CallDeferred();
    }

    private void OnMalusChestSpawnRequested() => Callable.From(() => SpawnChest(Rarity.Cursed)).CallDeferred();

    private void SpawnChest(Rarity rarity)
    {
        if (!IsInstanceValid(this) || !IsInsideTree() || !_generated)
        {
            return;
        }
        var chest = new Chest { Rarity = rarity, Cell = new Vector2I(-1, -1) };
        PlaceChest(chest, keepCell: false);
        chest.Opened += OnChestOpened;
        _chests.Add(chest);
        _chestsContainer.AddChild(chest);
        ChestSpawned?.Invoke(chest);
    }

    private void OnChestOpened(Chest chest)
    {
        _chests.Remove(chest);
        _occupiedCells.Remove(chest.Cell);
        ChestOpened?.Invoke(chest);
        chest.QueueFree();
    }

    // ---------------------------------------------------------------- run events

    private void OnPalierStarted(PalierDef palier)
    {
        ClearChildren(_ballsContainer);
        Generate();
    }

    private void OnStatsChanged()
    {
        if (!_generated)
        {
            return;
        }

        var stats = RunManager.Instance.Stats;
        var effective = BuildEffectiveStats();

        if (effective.RowCount != _builtRows)
        {
            Generate();
            return;
        }

        _effective = effective;
        if (!Mathf.IsEqualApprox(effective.SlotWidthModifier, _builtSlotWidth))
        {
            _builtSlotWidth = effective.SlotWidthModifier;
            GenerateSlots();
        }

        // A new wild blocker (malus) drops in at random — everything else stays where it was.
        while (_builtBlockers < stats.BlockerCount)
        {
            AddBlocker(0f);
            _builtBlockers++;
        }
    }

    private void OnBoardActionRequested(BoardAction action)
    {
        if (action is BoardAction.PlaceBlocker or BoardAction.PlacePortal)
        {
            PlacementRequested?.Invoke(action == BoardAction.PlacePortal ? PlaceableKind.Portal : PlaceableKind.Blocker);
            return;
        }
        if (_slots.Count == 0)
        {
            return;
        }

        int index;
        float value;
        var rng = new Random();
        switch (action)
        {
            case BoardAction.ReplaceWorstSlot:
                index = IndexOfExtreme(worst: true);
                value = GoodReplacementValues[rng.Next(GoodReplacementValues.Length)];
                break;
            case BoardAction.BoostBestSlot:
                index = IndexOfExtreme(worst: false);
                value = Mathf.Round(_slotMultipliers[index] * 1.5f);
                break;
            case BoardAction.CurseGoodSlot:
                var candidates = new List<int>();
                int best = IndexOfExtreme(worst: false);
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slotMultipliers[i] > 1f && i != best)
                    {
                        candidates.Add(i);
                    }
                }
                if (candidates.Count == 0)
                {
                    return;
                }
                index = candidates[rng.Next(candidates.Count)];
                value = 0.5f;
                break;
            default:
                return;
        }

        GD.Print($"[Board] {action}: slot {index} x{_slotMultipliers[index]} -> x{value}");
        _slotMultipliers[index] = value;
        _slots[index].SetMultiplier(value, celebrate: true);
    }

    private int IndexOfExtreme(bool worst)
    {
        int found = 0;
        for (int i = 1; i < _slots.Count; i++)
        {
            bool better = worst ? _slotMultipliers[i] < _slotMultipliers[found] : _slotMultipliers[i] > _slotMultipliers[found];
            if (better)
            {
                found = i;
            }
        }
        return found;
    }

    // ---------------------------------------------------------------- dropping

    private float AimMin => _cx - _s * 1.6f;
    private float AimMax => _cx + _s * 1.6f;

    public void AimAtGlobal(Vector2 globalPosition)
    {
        _aimTargetX = Mathf.Clamp(ToLocal(globalPosition).X, AimMin, AimMax);
    }

    public void AimAtLocalX(float x)
    {
        _aimTargetX = Mathf.Clamp(x, AimMin, AimMax);
    }

    public float AimRangeMin => AimMin;
    public float AimRangeMax => AimMax;

    public bool TryDrop()
    {
        if (!_generated || !RunManager.Instance.TryConsumeBall())
        {
            return false;
        }

        SpawnBall(_aimX + (float)GD.RandRange(-0.06, 0.06) * _s);
        int extra = RunManager.Instance.Stats.ExtraFreeBalls;
        for (int i = 0; i < extra; i++)
        {
            RunManager.Instance.RegisterFreeBall();
            float side = i % 2 == 0 ? 1f : -1f;
            SpawnBall(_aimX + side * _s * (0.35f + 0.2f * (i / 2)));
        }

        _launcherPulse = 1f;
        Sfx.Play(Sound.Drop, 0.9f + GD.Randf() * 0.2f, -4f);
        return true;
    }

    private void SpawnBall(float x)
    {
        float radius = RunManager.Instance.Stats.BallRadius * Unit;
        x = Mathf.Clamp(x, WallLeft + radius + 2f, WallRight - radius - 2f);
        var ball = new Ball
        {
            Radius = radius,
            Position = new Vector2(x, LauncherY + _s * 0.2f),
            RunId = RunManager.Instance.RunId,
            LinearVelocity = new Vector2((float)GD.RandRange(-12.0, 12.0), 40f),
        };
        RunManager.Instance.NotifyBallSpawned(ball);
        _ballsContainer.AddChild(ball);
    }

    // ---------------------------------------------------------------- drawing

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _aimX = Mathf.Lerp(_aimX, _aimTargetX, 1f - Mathf.Exp(-(float)delta * 18f));
        _launcherPulse = Mathf.Max(0f, _launcherPulse - (float)delta * 4f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_generated)
        {
            return;
        }

        // Playfield glass, from launcher rail to floor.
        var field = new Rect2(WallLeft, LauncherY - _s * 0.5f, WallRight - WallLeft, FloorY - LauncherY + _s * 0.5f);
        Paint.VerticalGradient(this, field, new Color(0.07f, 0.03f, 0.1f, 0.85f), new Color(0.03f, 0.01f, 0.06f, 0.92f));

        // Soft triangular spotlight behind the pegs.
        var (firstStart, firstEnd) = GetRowRange(0);
        var (lastStart, lastEnd) = GetRowRange(_effective.RowCount - 1);
        var glowTop = new Color(0.55f, 0.2f, 0.7f, 0.16f);
        var glowBottom = new Color(0.3f, 0.1f, 0.5f, 0.05f);
        DrawPolygon(new[]
        {
            new Vector2(firstStart - _s, _top - _s * 0.5f), new Vector2(firstEnd + _s, _top - _s * 0.5f),
            new Vector2(lastEnd + _s, LastRowY + _s * 0.4f), new Vector2(lastStart - _s, LastRowY + _s * 0.4f),
        }, new[] { glowTop, glowTop, glowBottom, glowBottom });

        // Neon side rails.
        float railTop = LauncherY - _s * 0.5f;
        var rail = Pal.Hdr(Pal.Pink, 1.8f);
        DrawLine(new Vector2(WallLeft, railTop), new Vector2(WallLeft, FloorY), Pal.Alpha(Pal.Pink, 0.25f), 8f);
        DrawLine(new Vector2(WallRight, railTop), new Vector2(WallRight, FloorY), Pal.Alpha(Pal.Pink, 0.25f), 8f);
        DrawLine(new Vector2(WallLeft, railTop), new Vector2(WallLeft, FloorY), rail, 2.5f);
        DrawLine(new Vector2(WallRight, railTop), new Vector2(WallRight, FloorY), rail, 2.5f);

        // Slot bay floor line.
        DrawLine(new Vector2(WallLeft, FloorY - _s * 0.2f), new Vector2(WallRight, FloorY - _s * 0.2f), Pal.Alpha(Pal.Purple, 0.4f), 2f);

        DrawLauncher();
    }

    private void DrawLauncher()
    {
        float y = LauncherY;
        var railColor = Pal.Alpha(Pal.Cyan, 0.35f);
        DrawLine(new Vector2(AimMin - _s * 0.3f, y - _s * 0.35f), new Vector2(AimMax + _s * 0.3f, y - _s * 0.35f), railColor, 2f);
        for (float x = AimMin; x <= AimMax + 0.1f; x += _s * 0.4f)
        {
            DrawCircle(new Vector2(x, y - _s * 0.35f), 1.5f, Pal.Alpha(Pal.Cyan, 0.5f));
        }

        if (!LauncherActive)
        {
            return;
        }

        bool ready = RunManager.Instance.IsPlaying && RunManager.Instance.BallsRemaining > 0;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 5f);
        var color = ready ? Pal.Cyan : new Color(0.4f, 0.38f, 0.45f);

        // Aim guide down to the first row.
        if (ready)
        {
            DrawDashedLine(new Vector2(_aimX, y + _s * 0.3f), new Vector2(_aimX, _top - _s * 0.3f),
                Pal.Alpha(Pal.Hdr(Pal.Cyan, 1.3f), 0.35f + 0.2f * pulse), 1.5f, 5f);
        }

        // Nozzle
        float squash = 1f + 0.35f * _launcherPulse;
        var nozzle = new[]
        {
            new Vector2(_aimX - _s * 0.42f * squash, y - _s * 0.5f),
            new Vector2(_aimX + _s * 0.42f * squash, y - _s * 0.5f),
            new Vector2(_aimX + _s * 0.2f, y - _s * 0.05f),
            new Vector2(_aimX - _s * 0.2f, y - _s * 0.05f),
        };
        Paint.Halo(this, new Vector2(_aimX, y - _s * 0.25f), _s * 0.9f, Pal.Alpha(color, 0.25f + 0.3f * _launcherPulse));
        DrawColoredPolygon(nozzle, color.Darkened(0.55f));
        DrawPolyline(new[] { nozzle[0], nozzle[1], nozzle[2], nozzle[3], nozzle[0] }, Pal.Hdr(color, 1.6f + _launcherPulse), 2f, true);

        if (ready)
        {
            float r = RunManager.Instance.Stats.BallRadius * Unit;
            DrawCircle(new Vector2(_aimX, y + _s * 0.05f), r * 0.9f, Pal.Alpha(Pal.Hdr(Colors.White, 1.2f), 0.35f + 0.25f * pulse));
        }
    }

    private static void ClearChildren(Node container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}
