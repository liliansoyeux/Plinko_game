using Godot;
using System.Collections.Generic;

namespace Plinko;

// One run, start to finish: builds the machine scene, wires RunManager events to the board,
// HUD and effects, owns input and pausing. A fresh GameScreen is created for every run, so
// nothing from a previous run can leak into the next one.
public partial class GameScreen : Node2D
{
    public CharacterDef Character = Characters.Classic;

    public PlinkoBoard Board { get; private set; }
    public UpgradeChoicePopup Popup { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsUserPaused => _userPaused;
    public PauseMenu PauseMenu => _pause;
    public PlacementTool Placer => _placer != null && IsInstanceValid(_placer) ? _placer : null;
    public bool IsPlacing => Placer != null;
    public GameOverOverlay GameOverOverlay => _gameOver;

    private const double HoldInterval = 0.16;
    private const double MinDropInterval = 0.09;

    private MachineCabinet _cabinet;
    private ShakeCamera _camera;
    private Node2D _fx;
    private HudUI _hud;
    private Banner _banner;
    private PauseMenu _pause;
    private GameOverOverlay _gameOver;
    private ColorRect _flash;

    private readonly Queue<Rarity> _pendingChoices = new();
    private readonly Queue<PlaceableKind> _pendingPlacements = new();
    private PlacementTool _placer;
    private bool _choiceOpen;
    private bool _userPaused;
    private bool _fast;
    private bool _holding;
    private bool _chestHintShown;
    private bool _cursedHintShown;
    private double _holdTimer;
    private double _sinceLastDrop = 10.0;
    private int _runId;

    public override void _Ready()
    {
        AddChild(new CasinoBackground());
        _cabinet = new MachineCabinet();
        AddChild(_cabinet);

        var screen = MachineCabinet.Screen;
        Board = new PlinkoBoard { Area = new Rect2(screen.Position + new Vector2(10f, 4f), screen.Size - new Vector2(20f, 8f)) };
        AddChild(Board);
        Board.ChestOpened += OnChestOpened;
        Board.ChestSpawned += OnChestSpawned;
        Board.PlacementRequested += kind => _pendingPlacements.Enqueue(kind);
        Board.BallDuplicated += OnBallDuplicated;

        _fx = new Node2D { Name = "Fx", ZIndex = 30 };
        AddChild(_fx);

        AddChild(new LegsAndShoes { Character = Character });

        _camera = new ShakeCamera { Position = new Vector2(450f, 500f), IgnoreRotation = false };
        AddChild(_camera);
        _camera.MakeCurrent();

        _hud = new HudUI();
        AddChild(_hud);
        _hud.PausePressed += TogglePause;
        _hud.SpeedToggled += ToggleSpeed;

        var flashLayer = new CanvasLayer { Layer = 7 };
        AddChild(flashLayer);
        _flash = new ColorRect { Color = new Color(1f, 0.9f, 0.7f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        flashLayer.AddChild(_flash);

        _banner = new Banner();
        AddChild(_banner);

        Popup = new UpgradeChoicePopup();
        AddChild(Popup);

        _pause = new PauseMenu();
        AddChild(_pause);
        _pause.ResumePressed += TogglePause;
        _pause.RestartPressed += () => Main.Instance.StartGame(Character);
        _pause.MenuPressed += () => Main.Instance.ShowTitle();

        _gameOver = new GameOverOverlay();
        AddChild(_gameOver);
        _gameOver.RetryPressed += () => Main.Instance.StartGame(Character);
        _gameOver.MenuPressed += () => Main.Instance.ShowTitle();

        var run = RunManager.Instance;
        run.PalierStarted += OnPalierStarted;
        run.PalierCleared += OnPalierCleared;
        run.GameOver += OnGameOver;
        run.SlotScored += OnSlotScored;
        run.LeveledUp += OnLeveledUp;
        run.MalusTriggered += OnMalusTriggered;
        run.CocktailAdded += OnCocktailAdded;
        run.SecondChanceUsed += OnSecondChance;

        run.StartNewRun(Character);
        _runId = run.RunId;

        if (SkillTree.Has("fortune_portal"))
        {
            // Skill-tree capstone: every run opens with a portal to place.
            run.Stats.PortalCount++;
            _pendingPlacements.Enqueue(PlaceableKind.Portal);
            _choiceOpen = true;
            Callable.From(() =>
            {
                UpdatePause();
                ContinueAfterChoice();
            }).CallDeferred();
        }
    }

    public override void _ExitTree()
    {
        var run = RunManager.Instance;
        run.PalierStarted -= OnPalierStarted;
        run.PalierCleared -= OnPalierCleared;
        run.GameOver -= OnGameOver;
        run.SlotScored -= OnSlotScored;
        run.LeveledUp -= OnLeveledUp;
        run.MalusTriggered -= OnMalusTriggered;
        run.CocktailAdded -= OnCocktailAdded;
        run.SecondChanceUsed -= OnSecondChance;
        GetTree().Paused = false;
        Engine.TimeScale = 1.0;
        Engine.PhysicsTicksPerSecond = 60;
    }

    // ---------------------------------------------------------------- input

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion:
                Board.AimAtGlobal(GetGlobalMousePosition());
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed)
                {
                    Board.AimAtGlobal(GetGlobalMousePosition());
                    Drop();
                    _holding = true;
                    _holdTimer = HoldInterval * 2.2;
                }
                else
                {
                    _holding = false;
                }
                break;

            case InputEventKey { Pressed: true } key:
                switch (key.Keycode)
                {
                    case Key.Space:
                    case Key.Enter:
                        Drop();
                        break;
                    case Key.Escape:
                    case Key.P:
                        if (!key.Echo) TogglePause();
                        break;
                    case Key.F:
                        if (!key.Echo) ToggleSpeed();
                        break;
                    case Key.M:
                        if (!key.Echo) _hud.ToggleSound();
                        break;
                    default:
                        return;
                }
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public override void _Process(double delta)
    {
        // Real (unscaled) time for input pacing, so x2 speed doesn't double the fire rate.
        double real = delta / Engine.TimeScale;
        _sinceLastDrop += real;

        if (_holding)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _holding = false;
            }
            else
            {
                _holdTimer -= real;
                if (_holdTimer <= 0)
                {
                    _holdTimer = HoldInterval;
                    Drop();
                }
            }
        }

        float axis = 0f;
        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Q)) axis -= 1f;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)) axis += 1f;
        if (axis != 0f)
        {
            Board.AimAtLocalX(Board.LauncherPosition.X + axis * 320f * (float)real);
        }
    }

    public bool Drop()
    {
        if (GetTree().Paused || IsGameOver || _sinceLastDrop < MinDropInterval)
        {
            return false;
        }
        if (Board.TryDrop())
        {
            _sinceLastDrop = 0;
            return true;
        }
        return false;
    }

    private void TogglePause()
    {
        if (IsGameOver || _choiceOpen)
        {
            return;
        }
        _userPaused = !_userPaused;
        if (_userPaused)
        {
            _pause.Open();
        }
        else
        {
            _pause.Close();
        }
        UpdatePause();
    }

    public void ToggleSpeed()
    {
        _fast = !_fast;
        Engine.TimeScale = _fast ? 2.0 : 1.0;
        // Keep the physics step at 1/60 s of game time so fast mode doesn't tunnel balls.
        Engine.PhysicsTicksPerSecond = _fast ? 120 : 60;
        _hud.SetSpeed(_fast);
    }

    private void UpdatePause()
    {
        GetTree().Paused = _choiceOpen || _userPaused;
    }

    // ---------------------------------------------------------------- run events

    private void OnPalierStarted(PalierDef palier)
    {
        _cabinet.BossMode = palier.IsBoss;
        Board.LauncherActive = true;
        int balls = RunManager.Instance.BallsRemaining;

        if (palier.IsBoss)
        {
            Sfx.Play(Sound.Boss);
            _camera.AddTrauma(0.5f);
            var mods = new List<string>();
            foreach (var modifier in palier.Modifiers)
            {
                mods.Add(modifier.Label);
            }
            _banner.Show("BOSS !", $"Palier {palier.Index + 1}  ·  {string.Join("  ·  ", mods)}\nObjectif {Pal.FormatScore(palier.ScoreTarget)} en {balls} billes", Pal.Red, 2.2f);
        }
        else
        {
            _banner.Show($"PALIER {palier.Index + 1}", $"Objectif : {Pal.FormatScore(palier.ScoreTarget)} points en {balls} billes", Pal.Cyan, 1.0f);
        }
    }

    private void OnPalierCleared(PalierDef palier)
    {
        Sfx.Play(Sound.PalierClear);
        _cabinet.Celebrate();
        Board.LauncherActive = false;
        _banner.Show("PALIER RÉUSSI !", $"{Pal.FormatScore(RunManager.Instance.Score)} / {Pal.FormatScore(palier.ScoreTarget)}", Pal.Green, 1.1f);

        var colors = new[] { Pal.Gold, Pal.Pink, Pal.Cyan, Pal.Green };
        for (int i = 0; i < 6; i++)
        {
            Fx.Burst(_fx, new Vector2(120f + i * 132f, 760f), colors[i % colors.Length], 30, 520f, 1.2f, 35f, Vector2.Up, 1.2f);
        }

        int runId = _runId;
        // Not processAlways: opening the pause menu during the celebration holds the next palier.
        GetTree().CreateTimer(2.0, processAlways: false).Timeout += () =>
        {
            if (IsInstanceValid(this) && RunManager.Instance.RunId == runId)
            {
                RunManager.Instance.AdvanceToNextPalier();
            }
        };
    }

    private void OnGameOver(PalierDef palier)
    {
        IsGameOver = true;
        Board.LauncherActive = false;
        _holding = false;
        _pendingChoices.Clear();
        Sfx.Play(Sound.GameOver);
        _camera.AddTrauma(0.4f);

        GetTree().CreateTimer(0.9).Timeout += () =>
        {
            if (IsInstanceValid(this))
            {
                _gameOver.Display();
            }
        };
    }

    private void OnSlotScored(SlotHit hit)
    {
        var pos = hit.Ball != null && IsInstanceValid(hit.Ball) ? hit.Ball.GlobalPosition : new Vector2(450f, 760f);

        if (hit.IsMiss)
        {
            Fx.FloatText(_fx, pos + new Vector2(0f, -10f), "RATÉ", new Color(0.6f, 0.55f, 0.7f), 18, 40f);
            Sfx.Play(Sound.Miss, 1f, -3f);
            return;
        }

        bool golden = hit.Ball != null && IsInstanceValid(hit.Ball) && hit.Ball.IsGolden;
        var color = golden ? Pal.Gold : Pal.ForMultiplier(hit.Multiplier);
        string text = "+" + FormatPayout(hit.Payout);

        if (hit.IsBad)
        {
            Fx.FloatText(_fx, pos + new Vector2(0f, -14f), text, color, 17, 36f, 0.8f);
            Fx.Burst(_fx, pos, color, 6, 120f, 0.4f);
            Sfx.Play(Sound.BadSlot, 0.95f + GD.Randf() * 0.1f, -5f);
        }
        else if (hit.Multiplier >= 10f)
        {
            bool mega = hit.Multiplier >= 60f;
            Fx.FloatText(_fx, pos + new Vector2(0f, -26f), text, color, mega ? 46 : 34, 110f, 1.5f);
            Fx.Burst(_fx, pos, color, mega ? 70 : 45, mega ? 560f : 420f, 1.0f, 70f, Vector2.Up, 1.3f);
            Fx.Burst(_fx, pos, Pal.Gold, 30, 300f, 0.9f);
            _camera.AddTrauma(mega ? 0.8f : 0.45f);
            Sfx.Play(Sound.Jackpot, mega ? 1f : 1.12f);
            if (mega)
            {
                FlashScreen(0.28f);
            }
        }
        else
        {
            Fx.FloatText(_fx, pos + new Vector2(0f, -18f), text, color, hit.Multiplier > 1f ? 24 : 20, 55f);
            Fx.Burst(_fx, pos, color, 14, 220f, 0.6f, 60f);
            float pitch = 0.9f + 0.14f * Mathf.Log(Mathf.Max(1f, hit.Multiplier)) / Mathf.Log(2f);
            Sfx.Play(Sound.Slot, pitch, -3f);
        }

        if (golden)
        {
            Fx.Burst(_fx, pos, Pal.Gold, 20, 260f, 0.8f);
        }
    }

    private static string FormatPayout(float payout)
    {
        return payout < 10f ? $"{payout:0.#}".Replace(',', '.') : Pal.FormatScore(payout);
    }

    private void OnLeveledUp(int level)
    {
        if (level <= 1)
        {
            return;
        }
        Sfx.Play(Sound.LevelUp);
        _hud.FlashLevel();
        Fx.FloatText(_fx, new Vector2(344f, 838f), $"NIVEAU {level} !", Pal.Green, 22, 30f, 1.2f);
        Fx.Burst(_fx, new Vector2(344f, 840f), Pal.Green, 20, 260f, 0.7f, 50f);
    }

    private void OnMalusTriggered(int level)
    {
        if (level <= 0)
        {
            return;
        }
        Sfx.Play(Sound.Malus);
        _hud.FlashMalus();
        _camera.AddTrauma(0.25f);
        Fx.FloatText(_fx, new Vector2(556f, 838f), "COFFRE MAUDIT !", Pal.Purple, 20, 30f, 1.3f);
        Fx.Burst(_fx, new Vector2(556f, 840f), Pal.Purple, 20, 260f, 0.7f, 50f);
    }

    private void OnSecondChance()
    {
        Sfx.Play(Sound.LevelUp, 0.8f);
        _camera.AddTrauma(0.3f);
        _banner.Show("SECONDE CHANCE !", "+3 billes : il te manque si peu...", Pal.Green, 1.4f);
    }

    private void OnCocktailAdded(IRunModifier cocktail)
    {
        if (!IsNodeReady())
        {
            return;
        }
        Fx.FloatText(_fx, new Vector2(180f, 110f), cocktail.DisplayName, cocktail.LiquidColor, 20, 30f, 1.8f);
    }

    private void FlashScreen(float strength)
    {
        _flash.Color = new Color(1f, 0.9f, 0.7f, strength);
        CreateTween().TweenProperty(_flash, "color:a", 0f, 0.35f);
    }

    // ---------------------------------------------------------------- chests

    // First chest of each kind in a run gets a nudge, so new players learn to aim for them.
    private void OnChestSpawned(Chest chest)
    {
        if (chest.IsCursed ? _cursedHintShown : _chestHintShown)
        {
            return;
        }
        if (chest.IsCursed)
        {
            _cursedHintShown = true;
            Fx.FloatText(_fx, chest.Position + new Vector2(0f, -26f), "Coffre maudit : évite-le... ou pas !", Pal.Purple, 16, 30f, 2.6f);
        }
        else
        {
            _chestHintShown = true;
            Fx.FloatText(_fx, chest.Position + new Vector2(0f, -26f), "Touche le coffre avec une bille !", Pal.Gold, 16, 30f, 2.6f);
        }
    }

    private void OnChestOpened(Chest chest)
    {
        var color = chest.IsCursed ? Pal.Purple : Pal.ForRarity(chest.Rarity);
        Fx.Burst(_fx, chest.GlobalPosition, color, 40, 340f, 0.9f);
        Fx.Burst(_fx, chest.GlobalPosition, Pal.Gold, 16, 200f, 0.6f);
        _camera.AddTrauma(0.2f);
        Sfx.Play(chest.IsCursed ? Sound.CursedChest : Sound.Chest);
        EnqueueChoice(chest.Rarity);
    }

    public void EnqueueChoice(Rarity rarity)
    {
        if (IsGameOver)
        {
            return;
        }
        _pendingChoices.Enqueue(rarity);
        if (!_choiceOpen)
        {
            // Deferred: chests open from a physics callback; let that frame finish first.
            Callable.From(OpenNextChoice).CallDeferred();
            _choiceOpen = true;
        }
    }

    private void OpenNextChoice()
    {
        if (!IsInstanceValid(this) || IsGameOver || _pendingChoices.Count == 0)
        {
            _choiceOpen = false;
            UpdatePause();
            return;
        }

        _choiceOpen = true;
        _holding = false;
        if (_userPaused)
        {
            _userPaused = false;
            _pause.Close();
        }
        UpdatePause();

        var run = RunManager.Instance;
        var rarity = _pendingChoices.Dequeue();
        bool cursed = rarity == Rarity.Cursed;
        List<UpgradeOption> Roll() => cursed
            ? MalusCatalog.PickRandom(3, run.Stats, run.Record)
            : UpgradeCatalog.PickForChest(rarity, run.Stats.ChestChoices, run.Stats, run.Record);
        var options = Roll();

        if (options.Count == 0)
        {
            OpenNextChoice();
            return;
        }

        string title = cursed ? "COFFRE MAUDIT" : $"COFFRE {Pal.RarityLabel(rarity)}";
        string subtitle = cursed ? "Aucune échappatoire... choisis le moindre mal." : "Choisis une amélioration pour ta partie.";
        Popup.Open(title, subtitle, cursed ? Pal.Purple : Pal.ForRarity(rarity), options, option =>
        {
            run.ApplyUpgrade(option);
            ContinueAfterChoice();
        }, cursed ? null : () =>
        {
            // Rerolls (skill tree) are a per-run budget, spent from the stats.
            if (run.Stats.Rerolls <= 0)
            {
                return null;
            }
            run.Stats.Rerolls--;
            return Roll();
        }, () => run.Stats.Rerolls);
    }

    // Debug/autopilot: apply an upgrade as if picked from a chest (placement included).
    public void EnqueueDebugUpgrade(UpgradeOption option)
    {
        RunManager.Instance.ApplyUpgrade(option);
        if (!_choiceOpen)
        {
            _choiceOpen = true;
            UpdatePause();
            ContinueAfterChoice();
        }
    }

    // Placeable upgrades (bonus blocker, portal) are positioned by the player before the
    // game resumes or the next chest opens. The tree stays paused throughout.
    private void ContinueAfterChoice()
    {
        if (IsGameOver || _pendingPlacements.Count == 0)
        {
            OpenNextChoice();
            return;
        }

        _placer = new PlacementTool { Board = Board, Kind = _pendingPlacements.Dequeue() };
        Board.LauncherActive = false;
        _placer.Placed += () =>
        {
            _placer = null;
            Board.LauncherActive = !IsGameOver;
            ContinueAfterChoice();
        };
        Board.AddChild(_placer);
    }

    private void OnBallDuplicated(Vector2 position)
    {
        Fx.Burst(_fx, position, Pal.Prismatic((float)Time.GetTicksMsec() / 1000f), 18, 240f, 0.6f);
        Sfx.Play(Sound.Portal, 1f + GD.Randf() * 0.3f, -6f);
    }
}
