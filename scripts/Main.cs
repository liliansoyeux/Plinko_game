using Godot;
using System;

namespace Plinko;

// Root of the game: owns the post-processing environment (HDR glow), the fade layer and
// the current screen (title or the game), and swaps between them.
public partial class Main : Node2D
{
    public static Main Instance { get; private set; }

    private Node _screen;
    private ColorRect _fade;
    private bool _transitioning;
    private Tween _fadeTween;

    public IdleTitleScreen Title => _screen as IdleTitleScreen;
    public IdleGameScreen Game => _screen as IdleGameScreen;
    public bool IsTransitioning => _transitioning;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        // Theme inheritance stops at CanvasLayer/Node2D parents, so a theme on the root window
        // never reaches the HUD or menus. Merging into the engine default theme applies it
        // to every control in the game.
        var defaultTheme = ThemeDB.GetDefaultTheme();
        defaultTheme.MergeWith(UiTheme.Build());
        defaultTheme.DefaultFont = Fonts.Regular;
        defaultTheme.DefaultFontSize = 18;

        AddChild(new WorldEnvironment { Environment = BuildEnvironment() });

        var fadeLayer = new CanvasLayer { Layer = 50 };
        AddChild(fadeLayer);
        _fade = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        fadeLayer.AddChild(_fade);

        SwapTo(new IdleTitleScreen());
        FadeFromBlack(0.6f);

        if (Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith("--autopilot")))
        {
            AddChild(new AutoPilot());
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            IdleManager.Instance?.Save();
        }
    }

    private static Godot.Environment BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Canvas,
            BackgroundCanvasMaxLayer = 3,
            GlowEnabled = true,
            GlowIntensity = 0.85f,
            GlowStrength = 1.05f,
            GlowBloom = 0f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
            GlowHdrThreshold = 1.0f,
            GlowHdrScale = 2f,
        };
        env.SetGlowLevel(0, 0f);
        env.SetGlowLevel(1, 0.6f);
        env.SetGlowLevel(2, 1f);
        env.SetGlowLevel(3, 0.8f);
        env.SetGlowLevel(4, 0.6f);
        env.SetGlowLevel(5, 0f);
        env.SetGlowLevel(6, 0f);
        return env;
    }

    public void ShowTitle() => TransitionTo(() => new IdleTitleScreen());

    public void StartGame(string welcomeTitle = null, string welcomeText = null) =>
        TransitionTo(() => new IdleGameScreen { WelcomeTitle = welcomeTitle, WelcomeText = welcomeText });

    private void TransitionTo(Func<Node> factory)
    {
        if (_transitioning)
        {
            return;
        }
        _transitioning = true;

        // A still-running reveal fade (e.g. the title's opening one) would otherwise clear
        // _transitioning mid-way through this transition and let a second one overlap.
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.SetPauseMode(Tween.TweenPauseMode.Process);
        _fadeTween.TweenProperty(_fade, "color:a", 1f, 0.22f);
        _fadeTween.TweenCallback(Callable.From(() =>
        {
            SwapTo(factory());
            FadeFromBlack(0.3f);
        }));
    }

    private void SwapTo(Node screen)
    {
        if (_screen != null)
        {
            RemoveChild(_screen);
            _screen.QueueFree();
        }
        GetTree().Paused = false;
        Engine.TimeScale = 1.0;
        _screen = screen;
        _screen.ProcessMode = ProcessModeEnum.Pausable;
        AddChild(_screen);
    }

    private void FadeFromBlack(float duration)
    {
        _fade.Color = new Color(0f, 0f, 0f, 1f);
        _fadeTween = CreateTween();
        _fadeTween.SetPauseMode(Tween.TweenPauseMode.Process);
        _fadeTween.TweenProperty(_fade, "color:a", 0f, duration);
        _fadeTween.TweenCallback(Callable.From(() => _transitioning = false));
    }
}
