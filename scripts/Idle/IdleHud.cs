using Godot;
using System;

namespace Plinko;

// HUD drawn on the machine itself: marquee (balls, income, frenzy/shoes) and ledge gauges
// (progress to the next jeton and to the next pair of shoes), plus sound/pause buttons.
public partial class IdleHud : CanvasLayer
{
    public event Action PausePressed;

    public IdleBoard Board;
    private Button _sound;
    private SoundMenu _soundMenu;

    public override void _Ready()
    {
        Layer = 3;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var canvas = new IdleHudCanvas { Board = Board, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(canvas);

        var buttons = new HBoxContainer { Position = new Vector2(746f, 18f), MouseFilter = Control.MouseFilterEnum.Ignore };
        buttons.AddThemeConstantOverride("separation", 8);
        root.AddChild(buttons);
        _sound = SmallButton("♪", "Son et musique");
        _sound.Pressed += ToggleSoundMenu;
        buttons.AddChild(_sound);
        var pause = SmallButton("II", "Pause (Échap)");
        pause.Pressed += () => PausePressed?.Invoke();
        buttons.AddChild(pause);

        // Sound menu, opened with the ♪ button: music volume gauge + mute switch.
        _soundMenu = new SoundMenu { Position = new Vector2(596f, 66f), Visible = false };
        root.AddChild(_soundMenu);

        var hint = Ui.Label("Viser : souris   ·   Clic / Espace : lâcher une bille   ·   A : lâcher auto   ·   Échap : pause", 13, Pal.Alpha(Pal.TextDim, 0.75f), Fonts.Regular, HorizontalAlignment.Center);
        hint.Position = new Vector2(0f, 972f);
        hint.Size = new Vector2(900f, 20f);
        root.AddChild(hint);
        RefreshSound();
    }

    public static Button SmallButton(string text, string tooltip)
    {
        var button = new Button { Text = text, TooltipText = tooltip, CustomMinimumSize = new Vector2(52f, 40f), FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0.08f, 0.04f, 0.12f, 0.9f), Pal.Alpha(Pal.Cyan, 0.5f), 2, 10, 4));
        var hover = UiTheme.Box(new Color(0.15f, 0.07f, 0.22f, 0.95f), Pal.Cyan, 2, 10, 4);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.MouseEntered += () => Sfx.Play(Sound.Hover);
        button.Pressed += () => Sfx.Play(Sound.Click);
        return button;
    }

    public void ToggleSound()
    {
        bool muted = !Sfx.Muted;
        Sfx.SetMuted(muted);
        SaveData.Muted = muted;
        RefreshSound();
    }

    public void ToggleSoundMenu()
    {
        _soundMenu.Visible = !_soundMenu.Visible;
        _soundMenu.Refresh();
    }

    // Debug/autopilot: the sound menu's music slider.
    public HSlider MusicSlider => _soundMenu.Slider;

    public void ToggleAutoDrop()
    {
        var idle = IdleManager.Instance;
        if (!idle.HasAutoDropper) return;
        idle.AutoDropEnabled = !idle.AutoDropEnabled;
        idle.Save();
    }

    private void RefreshSound()
    {
        _sound.Modulate = Sfx.Muted ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
        _soundMenu?.Refresh();
    }
}

// Small drop-down under the ♪ button.
public partial class SoundMenu : PanelContainer
{
    public HSlider Slider { get; private set; }
    private Label _value;
    private CheckButton _mute;
    private bool _refreshing;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(262f, 0f);
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.07f, 0.03f, 0.1f, 1f), Pal.Cyan, 2, 12, 14));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        AddChild(column);

        var header = new HBoxContainer();
        header.AddChild(Ui.Label("MUSIQUE", 16, Pal.Cyan, Fonts.Bold));
        _value = Ui.Label("", 16, Pal.Text, Fonts.Bold, HorizontalAlignment.Right);
        _value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_value);
        column.AddChild(header);

        Slider = new HSlider
        {
            MinValue = 0, MaxValue = 100, Step = 5,
            CustomMinimumSize = new Vector2(230f, 28f),
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
        };
        var track = UiTheme.Box(new Color(0.16f, 0.1f, 0.22f), Pal.Alpha(Pal.Cyan, 0.4f), 1, 5, 0);
        track.ContentMarginTop = track.ContentMarginBottom = 4;
        var fill = UiTheme.Box(Pal.Alpha(Pal.Cyan, 0.75f), Pal.Cyan, 1, 5, 0);
        fill.ContentMarginTop = fill.ContentMarginBottom = 4;
        Slider.AddThemeStyleboxOverride("slider", track);
        Slider.AddThemeStyleboxOverride("grabber_area", fill);
        Slider.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        Slider.ValueChanged += OnSlider;
        column.AddChild(Slider);

        _mute = new CheckButton { Text = "Couper tout le son (M)", FocusMode = FocusModeEnum.None };
        _mute.AddThemeFontSizeOverride("font_size", 15);
        _mute.Toggled += muted =>
        {
            if (_refreshing) return;
            Sfx.SetMuted(muted);
            SaveData.Muted = muted;
        };
        column.AddChild(_mute);
        Refresh();
    }

    private void OnSlider(double value)
    {
        _value.Text = $"{value:0}%";
        if (_refreshing) return;
        Sfx.SetMusicVolume((float)(value / 100.0));
    }

    public void Refresh()
    {
        if (Slider == null) return;
        _refreshing = true;
        Slider.Value = Math.Round(Sfx.MusicVolume * 100.0);
        _value.Text = $"{Slider.Value:0}%";
        _mute.ButtonPressed = Sfx.Muted;
        _refreshing = false;
    }
}

public partial class IdleHudCanvas : Control
{
    public IdleBoard Board;
    private double _shownIncome;
    private float _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        var idle = IdleManager.Instance;
        _shownIncome += (idle.IncomePerSecond - _shownIncome) * (1.0 - Math.Exp(-delta * 4.0));
        QueueRedraw();
    }

    public override void _Draw()
    {
        var idle = IdleManager.Instance;
        var m = MachineCabinet.Marquee;
        float midY = m.Position.Y + m.Size.Y / 2f;
        var bold = Fonts.Bold;
        var black = Fonts.Black;

        foreach (float x in new[] { 248f, 652f })
        {
            DrawLine(new Vector2(x, m.Position.Y + 16f), new Vector2(x, m.End.Y - 16f), Pal.Alpha(Pal.Gold, 0.25f), 1.5f);
        }

        // Current forged ball.
        var ball = BallTiers.All[idle.BallTier];
        var ballGlow = idle.BallTier == 5 ? Pal.Prismatic(_time) : ball.Glow;
        Paint.TextCentered(this, bold, new Vector2(148f, midY - 22f), "TA BILLE", 14, Pal.Alpha(Pal.Pink, 0.9f));
        Paint.TextCentered(this, black, new Vector2(148f, midY + 2f), ball.Name.Replace("Bille d'", "").Replace("Bille de ", "").Replace("Bille ", "").ToUpper() + $" {idle.BallLevel}", 26, Pal.Hdr(ballGlow, 1.1f), 4);
        string worth = Big.Format(ball.Value * idle.BallLevelMultiplier);
        string rate = idle.HasAutoDropper ? $"niv {idle.BallLevel}/10 · x{worth} · gravité x{idle.BallSpeed:0.##}" : $"niv {idle.BallLevel}/10 · x{worth} · clique pour lâcher";
        Paint.TextCentered(this, bold, new Vector2(148f, midY + 26f), rate, 12, Pal.TextDim);

        // Income.
        bool frenzy = idle.FrenzyTimeLeft > 0;
        var incomeColor = frenzy ? Pal.Prismatic(_time) : Pal.Gold;
        if (_shownIncome < 0 && !frenzy) incomeColor = Pal.Red;
        Paint.TextCentered(this, bold, new Vector2(450f, midY - 26f), frenzy ? "GAINS · FRÉNÉSIE x7" : "GAINS", 14, Pal.Alpha(incomeColor, 0.95f));
        Paint.TextCentered(this, black, new Vector2(450f, midY + 2f), $"{(_shownIncome >= 0 ? "+" : "")}{Big.Format(_shownIncome)} /s", 34, Pal.Hdr(incomeColor, 1.25f), 4);
        Paint.TextCentered(this, bold, new Vector2(450f, midY + 26f), $"multiplicateur global x{Big.Format(idle.GlobalMultiplier)}", 12, Pal.TextDim);

        // Shoes or frenzy timer.
        if (frenzy)
        {
            Paint.TextCentered(this, bold, new Vector2(752f, midY - 22f), "FRÉNÉSIE", 14, Pal.Alpha(incomeColor, 0.95f));
            Paint.TextCentered(this, black, new Vector2(752f, midY + 10f), $"{Math.Ceiling(idle.FrenzyTimeLeft):0}s", 38, Pal.Hdr(incomeColor, 1.3f), 4);
        }
        else
        {
            Paint.TextCentered(this, bold, new Vector2(752f, midY - 22f), "CHAUSSURES", 14, Pal.Alpha(Pal.Cyan, 0.9f));
            Paint.TextCentered(this, bold, new Vector2(752f, midY + 2f), idle.Shoe.Name, 20, Pal.Hdr(idle.Shoe.ShoeColor.Lightened(0.3f), 1.1f), 4);
            Paint.TextCentered(this, bold, new Vector2(752f, midY + 26f), $"jetons x{idle.Shoe.JetonMultiplier:0}", 12, Pal.TextDim);
        }

        // Ledge gauges.
        // Progress toward the next prestige milestone (log scale: each one is 10x the last).
        double next = idle.NextMilestone;
        double previous = idle.PreviousMilestone;
        float jetonRatio = previous <= 0
            ? (float)Math.Clamp(idle.RunEarned / next, 0, 1)
            : (float)Math.Clamp(Math.Log10(Math.Max(1, idle.RunEarned) / previous) / Math.Log10(next / previous), 0, 1);
        if (!idle.HasNextMilestone) jetonRatio = 1f;
        string gaugeText = idle.PrestigeRank == 0 ? $"PRESTIGE À {Big.Format(next)}" : $"PALIER {idle.PrestigeRank} · +{idle.JetonsForPrestige} JETONS";
        DrawGauge(new Rect2(246f, 822f, 196f, 36f), gaugeText, jetonRatio, Pal.Purple);

        if (idle.UnlockedShoes < Characters.All.Count)
        {
            var target = Characters.All[idle.UnlockedShoes];
            float shoeRatio = idle.ShoeIndex >= idle.UnlockedShoes - 1
                ? (float)Math.Clamp(Math.Log10(Math.Max(1, idle.RunEarned)) / Math.Log10(target.UnlockRunEarnings), 0, 1)
                : 0f;
            DrawGauge(new Rect2(458f, 822f, 196f, 36f), $"PROCHAINE PAIRE : {Big.Format(target.UnlockRunEarnings)}", shoeRatio, Pal.Cyan);
        }
        else
        {
            DrawGauge(new Rect2(458f, 822f, 196f, 36f), "TOUTES LES PAIRES !", 1f, Pal.Gold);
        }
    }

    private void DrawGauge(Rect2 rect, string label, float ratio, Color color)
    {
        DrawColoredPolygon(Paint.RoundedRect(rect, 8f), new Color(0.04f, 0.02f, 0.06f, 0.85f));
        DrawString(Fonts.Bold, rect.Position + new Vector2(10f, 14f), label, HorizontalAlignment.Left, rect.Size.X - 16f, 12, Pal.Hdr(color, 1.1f));
        var bar = new Rect2(rect.Position + new Vector2(8f, 20f), new Vector2(rect.Size.X - 16f, 9f));
        DrawColoredPolygon(Paint.RoundedRect(bar, 4.5f), new Color(0.02f, 0.01f, 0.04f));
        if (ratio > 0.01f)
        {
            var fill = new Rect2(bar.Position, new Vector2(Mathf.Max(9f, bar.Size.X * ratio), bar.Size.Y));
            DrawColoredPolygon(Paint.RoundedRect(fill, 4.5f), Pal.Hdr(color, 1.2f));
        }
    }
}
