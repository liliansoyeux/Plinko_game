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
    private Button _auto;

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
        _auto = SmallButton("AUTO", "Lâcher automatique : marche / arrêt (A)");
        _auto.CustomMinimumSize = new Vector2(78f, 40f);
        _auto.AddThemeFontSizeOverride("font_size", 15);
        _auto.Pressed += ToggleAutoDrop;
        buttons.AddChild(_auto);
        buttons.Position = new Vector2(660f, 18f);
        _sound = SmallButton("♪", "Son (M)");
        _sound.Pressed += ToggleSound;
        buttons.AddChild(_sound);
        var pause = SmallButton("II", "Pause (Échap)");
        pause.Pressed += () => PausePressed?.Invoke();
        buttons.AddChild(pause);

        // Music volume gauge, top-left of the machine.
        var musicLabel = Ui.Label("MUSIQUE", 13, Pal.Alpha(Pal.Cyan, 0.85f), Fonts.Bold);
        musicLabel.Position = new Vector2(30f, 28f);
        musicLabel.Size = new Vector2(80f, 20f);
        root.AddChild(musicLabel);
        var music = new HSlider
        {
            Position = new Vector2(104f, 26f),
            Size = new Vector2(170f, 24f),
            MinValue = 0, MaxValue = 1, Step = 0.05,
            Value = Sfx.MusicVolume,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Volume de la musique",
        };
        music.AddThemeStyleboxOverride("slider", UiTheme.Box(new Color(0.08f, 0.04f, 0.12f, 0.9f), Pal.Alpha(Pal.Cyan, 0.5f), 1, 4, 3));
        music.AddThemeStyleboxOverride("grabber_area", UiTheme.Box(Pal.Alpha(Pal.Cyan, 0.7f), Pal.Cyan, 1, 4, 3));
        music.AddThemeStyleboxOverride("grabber_area_highlight", UiTheme.Box(Pal.Cyan, Pal.Cyan, 1, 4, 3));
        music.ValueChanged += v => Sfx.SetMusicVolume((float)v);
        root.AddChild(music);

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

    public void ToggleAutoDrop()
    {
        var idle = IdleManager.Instance;
        if (!idle.HasAutoDropper) return;
        idle.AutoDropEnabled = !idle.AutoDropEnabled;
        idle.Save();
    }

    public override void _Process(double delta)
    {
        var idle = IdleManager.Instance;
        _auto.Visible = idle.HasAutoDropper;
        bool on = idle.AutoDropEnabled;
        _auto.Text = on ? "AUTO" : "MANUEL";
        _auto.AddThemeColorOverride("font_color", on ? Pal.Green : Pal.TextDim);
        _auto.AddThemeColorOverride("font_hover_color", on ? Pal.Green.Lightened(0.3f) : Pal.Text);
    }

    private void RefreshSound() => _sound.Modulate = Sfx.Muted ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
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
        Paint.TextCentered(this, black, new Vector2(148f, midY + 2f), ball.Name.Replace("Bille d'", "").Replace("Bille de ", "").Replace("Bille ", "").ToUpper(), 26, Pal.Hdr(ballGlow, 1.1f), 4);
        string rate = idle.HasAutoDropper ? $"x{Big.Format(ball.Value)} · gravité x{idle.BallSpeed:0.##}" : $"x{Big.Format(ball.Value)} · clique pour lâcher";
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
        double next = idle.NextJetonAt;
        double previous = idle.JetonsForPrestige <= 0 ? 0 : Math.Pow(idle.JetonsForPrestige / idle.Shoe.JetonMultiplier, 3) * 1e6;
        float jetonRatio = (float)Math.Clamp((idle.RunEarned - previous) / Math.Max(1, next - previous), 0, 1);
        DrawGauge(new Rect2(246f, 822f, 196f, 36f), $"JETONS À GAGNER : {idle.JetonsForPrestige}", jetonRatio, Pal.Purple);

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
