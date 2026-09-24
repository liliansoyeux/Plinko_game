using Godot;
using System;

namespace Plinko;

// In-game HUD. The marquee (palier / objective / balls) and ledge gauges (level / malus)
// are custom-drawn onto the machine; cocktails and the small buttons are real controls.
public partial class HudUI : CanvasLayer
{
    public event Action PausePressed;
    public event Action SpeedToggled;

    private HudCanvas _canvas;
    private HBoxContainer _cocktails;
    private Button _speedButton;
    private Button _soundButton;

    public override void _Ready()
    {
        Layer = 3;

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _canvas = new HudCanvas { MouseFilter = Control.MouseFilterEnum.Ignore };
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_canvas);

        _cocktails = new HBoxContainer { Position = new Vector2(34f, 12f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _cocktails.AddThemeConstantOverride("separation", 2);
        root.AddChild(_cocktails);

        var buttons = new HBoxContainer { Position = new Vector2(686f, 18f), MouseFilter = Control.MouseFilterEnum.Ignore };
        buttons.AddThemeConstantOverride("separation", 8);
        root.AddChild(buttons);

        _speedButton = SmallButton("x1", "Vitesse (F)");
        _speedButton.Pressed += () => SpeedToggled?.Invoke();
        buttons.AddChild(_speedButton);

        _soundButton = SmallButton("♪", "Son (M)");
        _soundButton.Pressed += ToggleSound;
        buttons.AddChild(_soundButton);

        var pause = SmallButton("II", "Pause (Échap)");
        pause.Pressed += () => PausePressed?.Invoke();
        buttons.AddChild(pause);

        var hint = Ui.Label("Clic / Espace : lâcher une bille   ·   Maintenir : rafale   ·   Échap : pause", 13, Pal.Alpha(Pal.TextDim, 0.75f), Fonts.Regular, HorizontalAlignment.Center);
        hint.Position = new Vector2(0f, 972f);
        hint.Size = new Vector2(900f, 20f);
        root.AddChild(hint);

        var run = RunManager.Instance;
        // Created before StartNewRun, so the shelf fills purely from CocktailAdded events.
        run.CocktailAdded += OnCocktailAdded;
        RefreshSoundButton();
    }

    public override void _ExitTree()
    {
        RunManager.Instance.CocktailAdded -= OnCocktailAdded;
    }

    private static Button SmallButton(string text, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(52f, 40f),
            FocusMode = Control.FocusModeEnum.None,
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        var normal = UiTheme.Box(new Color(0.08f, 0.04f, 0.12f, 0.9f), Pal.Alpha(Pal.Cyan, 0.5f), 2, 10, 4);
        var hover = UiTheme.Box(new Color(0.15f, 0.07f, 0.22f, 0.95f), Pal.Cyan, 2, 10, 4);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.MouseEntered += () => Sfx.Play(Sound.Hover);
        button.Pressed += () => Sfx.Play(Sound.Click);
        return button;
    }

    public void SetSpeed(bool fast)
    {
        _speedButton.Text = fast ? "x2" : "x1";
        _speedButton.AddThemeColorOverride("font_color", fast ? Pal.Gold : Pal.Text);
    }

    public void ToggleSound()
    {
        bool muted = !Sfx.Muted;
        Sfx.SetMuted(muted);
        SaveData.SetMuted(muted);
        RefreshSoundButton();
    }

    private void RefreshSoundButton()
    {
        _soundButton.Modulate = Sfx.Muted ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
    }

    private void OnCocktailAdded(IRunModifier cocktail)
    {
        _cocktails.AddChild(new CocktailGlass { Cocktail = cocktail });
    }

    public void FlashLevel() => _canvas.FlashLevel();
    public void FlashMalus() => _canvas.FlashMalus();
}

public partial class HudCanvas : Control
{
    private float _shownScore;
    private float _shownXp;
    private float _shownMalus;
    private float _levelFlash;
    private float _malusFlash;
    private float _ballsBump;
    private float _goalFlash;
    private int _lastBalls = -1;
    private bool _goalReached;
    private float _time;

    public void FlashLevel() => _levelFlash = 1f;
    public void FlashMalus() => _malusFlash = 1f;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        var run = RunManager.Instance;
        float k = 1f - Mathf.Exp(-dt * 9f);

        _shownScore = Mathf.Lerp(_shownScore, run.Score, k);
        if (Mathf.Abs(_shownScore - run.Score) < 0.05f) _shownScore = run.Score;

        float xpRatio = run.XpToNextLevel > 0 ? run.Xp / run.XpToNextLevel : 0f;
        float malusRatio = run.NegativeXpToNext > 0 ? run.NegativeXp / run.NegativeXpToNext : 0f;
        // Snap down instantly on level-up so the bar visibly "empties" rather than rewinding.
        _shownXp = xpRatio < _shownXp - 0.3f ? xpRatio : Mathf.Lerp(_shownXp, xpRatio, k);
        _shownMalus = malusRatio < _shownMalus - 0.3f ? malusRatio : Mathf.Lerp(_shownMalus, malusRatio, k);

        if (run.BallsRemaining != _lastBalls)
        {
            if (_lastBalls >= 0) _ballsBump = 1f;
            _lastBalls = run.BallsRemaining;
        }

        bool reached = run.CurrentPalier != null && run.Score >= run.CurrentPalier.ScoreTarget;
        if (reached && !_goalReached)
        {
            _goalFlash = 1f;
            Sfx.Play(Sound.Pick, 1.2f, -2f);
        }
        _goalReached = reached;

        _levelFlash = Mathf.Max(0f, _levelFlash - dt * 1.5f);
        _malusFlash = Mathf.Max(0f, _malusFlash - dt * 1.5f);
        _ballsBump = Mathf.Max(0f, _ballsBump - dt * 5f);
        _goalFlash = Mathf.Max(0f, _goalFlash - dt * 1.2f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var run = RunManager.Instance;
        var palier = run.CurrentPalier;
        if (palier == null)
        {
            return;
        }

        var m = MachineCabinet.Marquee;
        float midY = m.Position.Y + m.Size.Y / 2f;
        var bold = Fonts.Bold;
        var black = Fonts.Black;

        // Separators.
        foreach (float x in new[] { 248f, 652f })
        {
            DrawLine(new Vector2(x, m.Position.Y + 16f), new Vector2(x, m.End.Y - 16f), Pal.Alpha(Pal.Gold, 0.25f), 1.5f);
        }

        // --- Palier
        var palierColor = palier.IsBoss ? Pal.Red : Pal.Cyan;
        Paint.TextCentered(this, bold, new Vector2(148f, midY - 22f), palier.IsBoss ? "PALIER BOSS" : "PALIER", 14, Pal.Alpha(palierColor, 0.85f));
        Paint.TextCentered(this, black, new Vector2(148f, midY + 10f), $"{palier.Index + 1}", 42, Pal.Hdr(palierColor, 1.3f), 4);

        // --- Objective
        float target = palier.ScoreTarget;
        float ratio = Mathf.Clamp(_shownScore / target, 0f, 1f);
        var goalColor = _goalReached ? Pal.Green : Pal.Gold;
        Paint.TextCentered(this, bold, new Vector2(450f, midY - 26f), _goalReached ? "OBJECTIF ATTEINT !" : "OBJECTIF", 14, Pal.Alpha(goalColor, 0.9f));
        string scoreText = $"{Pal.FormatScore(_shownScore)}";
        string targetText = $" / {Pal.FormatScore(target)}";
        var scoreSize = black.GetStringSize(scoreText, HorizontalAlignment.Left, -1, 32);
        var targetSize = bold.GetStringSize(targetText, HorizontalAlignment.Left, -1, 20);
        float startX = 450f - (scoreSize.X + targetSize.X) / 2f;
        float pop = 1f + 0.25f * _goalFlash;
        DrawString(black, new Vector2(startX, midY + 10f), scoreText, HorizontalAlignment.Left, -1, (int)(32 * pop), Pal.Hdr(goalColor, 1.2f + _goalFlash));
        DrawString(bold, new Vector2(startX + scoreSize.X, midY + 8f), targetText, HorizontalAlignment.Left, -1, 20, Pal.TextDim);

        var bar = new Rect2(272f, midY + 20f, 356f, 12f);
        DrawBar(bar, ratio, goalColor, _goalFlash);

        // --- Balls
        float bump = 1f + 0.3f * _ballsBump;
        var ballsColor = run.BallsRemaining == 0 ? Pal.TextDim : Pal.Text;
        Paint.TextCentered(this, bold, new Vector2(752f, midY - 22f), "BILLES", 14, Pal.Alpha(Pal.Pink, 0.9f));
        DrawSetTransform(new Vector2(752f, midY + 8f), 0f, new Vector2(bump, bump));
        Paint.TextCentered(this, black, Vector2.Zero, $"{run.BallsRemaining}", 38, Pal.Hdr(ballsColor, 1.1f), 4);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        int dots = Mathf.Min(run.BallsRemaining, 14);
        float dotsWidth = (dots - 1) * 11f;
        for (int i = 0; i < dots; i++)
        {
            DrawCircle(new Vector2(752f - dotsWidth / 2f + i * 11f, midY + 34f), 3.2f, Pal.Hdr(new Color(0.98f, 0.9f, 1f), 1.1f));
        }

        // --- Ledge gauges
        DrawGauge(new Rect2(246f, 822f, 196f, 36f), $"NIVEAU {run.Level}", _shownXp, Pal.Green, _levelFlash);
        DrawGauge(new Rect2(458f, 822f, 196f, 36f), $"MALUS {run.MalusLevel}", _shownMalus, Pal.Purple, _malusFlash);
    }

    private void DrawBar(Rect2 bar, float ratio, Color color, float flash)
    {
        DrawColoredPolygon(Paint.RoundedRect(bar, bar.Size.Y / 2f), new Color(0.02f, 0.01f, 0.04f));
        if (ratio > 0.001f)
        {
            var fill = new Rect2(bar.Position, new Vector2(Mathf.Max(bar.Size.Y, bar.Size.X * ratio), bar.Size.Y));
            DrawColoredPolygon(Paint.RoundedRect(fill.Grow(3f), fill.Size.Y / 2f + 3f), Pal.Alpha(color, 0.2f + 0.3f * flash));
            DrawColoredPolygon(Paint.RoundedRect(fill, fill.Size.Y / 2f), Pal.Hdr(color, 1.1f + flash));
            DrawLine(fill.Position + new Vector2(4f, 3f), new Vector2(fill.End.X - 4f, fill.Position.Y + 3f), new Color(1f, 1f, 1f, 0.35f), 2f);
        }
        DrawPolyline(Paint.Closed(Paint.RoundedRect(bar, bar.Size.Y / 2f)), new Color(1f, 1f, 1f, 0.15f), 1f, true);
    }

    private void DrawGauge(Rect2 rect, string label, float ratio, Color color, float flash)
    {
        DrawColoredPolygon(Paint.RoundedRect(rect, 8f), new Color(0.04f, 0.02f, 0.06f, 0.85f));
        if (flash > 0f)
        {
            DrawColoredPolygon(Paint.RoundedRect(rect.Grow(3f * flash), 10f), Pal.Alpha(color, 0.3f * flash));
        }
        DrawString(Fonts.Bold, rect.Position + new Vector2(10f, 14f), label, HorizontalAlignment.Left, -1, 13, Pal.Hdr(color, 1.1f + flash));
        DrawBar(new Rect2(rect.Position + new Vector2(8f, 20f), new Vector2(rect.Size.X - 16f, 9f)), Mathf.Clamp(ratio, 0f, 1f), color, flash);
    }
}
