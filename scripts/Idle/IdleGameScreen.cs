using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// The incremental game screen: the machine on the left, the shop on the right.
public partial class IdleGameScreen : Node2D
{
    public IdleBoard Board { get; private set; }
    public ShopPanel Shop { get; private set; }
    public SkillTreeOverlay SkillTree { get; private set; }
    public PlacementTool Placer => _placer != null && IsInstanceValid(_placer) ? _placer : null;
    public bool IsPlacing => Placer != null;
    public bool IsUserPaused => _userPaused;
    public PauseOverlay PauseMenu => _pause;

    // Set by Main when this screen follows a prestige, to celebrate the new pair.
    public string WelcomeTitle;
    public string WelcomeText;

    private const double HoldInterval = 0.1;

    private ShakeCamera _camera;
    private Node2D _fx;
    private MachineCabinet _cabinet;
    private IdleHud _hud;
    private Banner _banner;
    private PauseOverlay _pause;
    private PlacementTool _placer;
    private readonly Queue<PlaceableKind> _pendingPlacements = new();
    private bool _userPaused;
    private bool _holding;
    private double _holdTimer;
    private int _textsThisWindow;
    private int _bigTextsThisWindow;
    private double _textWindow;

    public override void _Ready()
    {
        var idle = IdleManager.Instance;
        AddChild(new CasinoBackground { ViewportSize = new Vector2(1500f, 1000f) });
        _cabinet = new MachineCabinet();
        AddChild(_cabinet);

        var screen = MachineCabinet.Screen;
        Board = new IdleBoard { Area = new Rect2(screen.Position + new Vector2(10f, 4f), screen.Size - new Vector2(20f, 8f)) };
        AddChild(Board);
        Board.Landed += OnLanded;
        Board.GoldenChestOpened += OnGoldenChest;
        Board.BallDuplicated += pos =>
        {
            Fx.Burst(_fx, pos, Pal.Prismatic((float)Time.GetTicksMsec() / 1000f), 12, 200f, 0.5f);
            Sfx.Play(Sound.Portal, 1.1f + GD.Randf() * 0.3f, -12f);
        };

        _fx = new Node2D { ZIndex = 30 };
        AddChild(_fx);
        AddChild(new LegsAndShoes { Character = idle.Shoe });

        _camera = new ShakeCamera { Position = new Vector2(750f, 500f), IgnoreRotation = false };
        AddChild(_camera);
        _camera.MakeCurrent();

        _hud = new IdleHud { Board = Board };
        AddChild(_hud);
        _hud.PausePressed += TogglePause;

        Shop = new ShopPanel();
        AddChild(Shop);
        Shop.OpenSkillTree += OpenSkillTree;
        Shop.PrestigeRequested += RequestPrestige;

        _banner = new Banner();
        AddChild(_banner);

        _pause = new PauseOverlay();
        AddChild(_pause);
        _pause.ResumePressed += TogglePause;
        _pause.MenuPressed += () => { idle.Save(); Main.Instance.ShowTitle(); };

        idle.PlacementRequested += OnPlacementRequested;
        idle.Announce += OnAnnounce;
        idle.AchievementUnlocked += OnAchievement;

        for (int i = 0; i < idle.PendingPortals; i++)
        {
            _pendingPlacements.Enqueue(PlaceableKind.Portal);
        }
        Callable.From(StartNextPlacement).CallDeferred();

        if (WelcomeTitle != null)
        {
            _banner.Show(WelcomeTitle, WelcomeText, idle.Shoe.ShoeColor.Lightened(0.3f), 2.2f);
        }
        if (idle.OfflineGain > 0)
        {
            var t = TimeSpan.FromSeconds(idle.OfflineSeconds);
            string duration = t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00}" : $"{t.Minutes} min";
            _banner.Show("BON RETOUR !", $"+{Big.Format(idle.OfflineGain)} pièces gagnées pendant ton absence ({duration})", Pal.Gold, 2.6f);
            idle.ConsumeOfflineReport();
        }
    }

    public override void _ExitTree()
    {
        var idle = IdleManager.Instance;
        idle.PlacementRequested -= OnPlacementRequested;
        idle.Announce -= OnAnnounce;
        idle.AchievementUnlocked -= OnAchievement;
        GetTree().Paused = false;
    }

    // ---------------------------------------------------------------- input

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion when motion.Position.X < ToScreenX(ShopPanel.PanelX):
                Board.AimAtGlobal(GetGlobalMousePosition());
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed && GetGlobalMousePosition().X < ShopPanel.PanelX)
                {
                    Board.AimAtGlobal(GetGlobalMousePosition());
                    Board.ManualDrop();
                    _holding = true;
                    _holdTimer = HoldInterval * 2;
                }
                else if (!mb.Pressed)
                {
                    _holding = false;
                }
                break;
            case InputEventKey { Pressed: true } key:
                switch (key.Keycode)
                {
                    case Key.Space: Board.ManualDrop(); break;
                    case Key.Escape: case Key.P: if (!key.Echo) TogglePause(); break;
                    case Key.M: if (!key.Echo) _hud.ToggleSound(); break;
                    default: return;
                }
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private float ToScreenX(float viewportX) => (GetViewport().GetScreenTransform() * new Vector2(viewportX, 0f)).X;

    public override void _Process(double delta)
    {
        if (_holding)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _holding = false;
            }
            else
            {
                _holdTimer -= delta;
                if (_holdTimer <= 0)
                {
                    _holdTimer = HoldInterval;
                    Board.ManualDrop();
                }
            }
        }

        float axis = 0f;
        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Q)) axis -= 1f;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)) axis += 1f;
        if (axis != 0f)
        {
            Board.AimAtLocalX(Board.LauncherPosition.X + axis * 320f * (float)delta);
        }

        _textWindow -= delta;
        if (_textWindow <= 0)
        {
            _textWindow = 0.5;
            _textsThisWindow = 0;
            _bigTextsThisWindow = 0;
        }
    }

    private void TogglePause()
    {
        if (IsPlacing || SkillTree != null)
        {
            return;
        }
        _userPaused = !_userPaused;
        if (_userPaused) _pause.Open(); else _pause.Close();
        UpdatePause();
    }

    private void UpdatePause() => GetTree().Paused = _userPaused || IsPlacing || SkillTree != null;

    // ---------------------------------------------------------------- feedback

    private void OnLanded(Slot slot, Ball ball, double payout, bool crit)
    {
        var pos = ball.GlobalPosition;
        var idle = IdleManager.Instance;
        bool big = crit || payout >= Math.Max(10.0, idle.IncomePerSecond * 0.5);
        var color = crit ? Pal.Gold : Pal.ForMultiplier(Mathf.Min(slot.Multiplier / (float)Math.Max(0.01, idle.SlotBoost), 200f));

        // With dozens of balls landing per second, only a few texts get through each half
        // second (big hits first) so they stay readable.
        bool showBig = big && _bigTextsThisWindow < 3;
        if (showBig || (!big && _textsThisWindow < 5))
        {
            if (showBig) _bigTextsThisWindow++; else _textsThisWindow++;
            string text = (crit ? "CRIT +" : "+") + Big.Format(payout);
            Fx.FloatText(_fx, pos + new Vector2(0f, -16f), text, color, crit ? 26 : big ? 24 : 18, big ? 70f : 45f, big ? 1.1f : 0.8f);
        }

        if (showBig)
        {
            Fx.Burst(_fx, pos, color, 30, 360f, 0.8f, 70f);
            _camera.AddTrauma(crit ? 0.35f : 0.2f);
            Sfx.Play(Sound.Jackpot, crit ? 1f : 1.15f, -4f);
        }
        else
        {
            Fx.Burst(_fx, pos, color, 5, 150f, 0.4f, 60f, null, 0.8f);
            Sfx.Play(Sound.Slot, 0.9f + 0.1f * ball.Tier + GD.Randf() * 0.1f, -9f);
        }
    }

    private void OnGoldenChest(Chest chest, string title, string detail)
    {
        Fx.Burst(_fx, chest.GlobalPosition, Pal.Gold, 60, 480f, 1.1f);
        _camera.AddTrauma(0.4f);
        _cabinet.Celebrate();
        Sfx.Play(Sound.PalierClear);
        _banner.Show(title, detail, Pal.Gold, 1.6f);
    }

    // Several achievements can unlock in the same instant: show them one after another.
    private readonly Queue<AchievementDef> _toasts = new();
    private AchievementToast _toast;

    private void OnAchievement(AchievementDef achievement)
    {
        _toasts.Enqueue(achievement);
        if (_toast == null)
        {
            ShowNextToast();
        }
    }

    private void ShowNextToast()
    {
        if (_toasts.Count == 0 || !IsInstanceValid(this))
        {
            _toast = null;
            return;
        }
        Sfx.Play(Sound.Pick, 1.2f);
        _toast = new AchievementToast { Achievement = _toasts.Dequeue() };
        _toast.TreeExited += ShowNextToast;
        AddChild(_toast);
    }

    private void OnAnnounce(string title, string text, Color color)
    {
        Sfx.Play(Sound.LevelUp);
        _cabinet.Celebrate();
        _banner.Show(title, text, color, 2.2f);
    }

    // ---------------------------------------------------------------- placement, skills, prestige

    private void OnPlacementRequested(PlaceableKind kind)
    {
        _pendingPlacements.Enqueue(kind);
        if (!IsPlacing)
        {
            StartNextPlacement();
        }
    }

    private void StartNextPlacement()
    {
        if (!IsInstanceValid(this) || IsPlacing || _pendingPlacements.Count == 0)
        {
            UpdatePause();
            return;
        }
        _placer = new PlacementTool { Board = Board, Kind = _pendingPlacements.Dequeue() };
        Board.LauncherActive = false;
        _placer.Placed += () =>
        {
            _placer = null;
            Board.LauncherActive = true;
            StartNextPlacement();
        };
        Board.AddChild(_placer);
        UpdatePause();
    }

    public void OpenSkillTreeOverlay() => OpenSkillTree();

    private void OpenSkillTree()
    {
        if (SkillTree != null || IsPlacing)
        {
            return;
        }
        SkillTree = new SkillTreeOverlay();
        SkillTree.Closed += () =>
        {
            SkillTree = null;
            UpdatePause();
        };
        AddChild(SkillTree);
        UpdatePause();
    }

    public void RequestPrestige(CharacterDef shoe)
    {
        int gain = IdleManager.Instance.JetonsForPrestige;
        if (gain < 1)
        {
            return;
        }
        Sfx.Play(Sound.PalierClear);
        IdleManager.Instance.Prestige(shoe);
        Main.Instance.StartGame($"+{gain} JETONS !", $"Nouvelle partie avec les {shoe.Name}. Ouvre l'arbre de compétences pour les dépenser.");
    }
}

public partial class PauseOverlay : CanvasLayer
{
    public event Action ResumePressed;
    public event Action MenuPressed;
    public Button ResumeButton { get; private set; }

    public override void _Ready()
    {
        Layer = 16;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        var dim = new ColorRect { Color = new Color(0.02f, 0f, 0.04f, 0.8f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(420f, 0f) };
        center.AddChild(panel);
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        vbox.AddChild(Ui.Label("PAUSE", 48, Pal.Cyan, Fonts.Black, HorizontalAlignment.Center, 6));
        vbox.AddChild(Ui.Label("La partie est sauvegardée automatiquement.", 15, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center));
        ResumeButton = MakeButton("Reprendre", () => ResumePressed?.Invoke());
        vbox.AddChild(ResumeButton);
        vbox.AddChild(MakeButton("Menu principal", () => MenuPressed?.Invoke()));
        vbox.AddChild(MakeButton("Quitter le jeu", () => { IdleManager.Instance.Save(); GetTree().Quit(); }));
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0f, 52f) };
        button.MouseEntered += () => Sfx.Play(Sound.Hover);
        button.Pressed += () => { Sfx.Play(Sound.Click); action(); };
        return button;
    }

    public void Open() => Visible = true;
    public void Close() => Visible = false;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Escape or Key.P)
        {
            GetViewport().SetInputAsHandled();
            ResumePressed?.Invoke();
        }
    }
}
