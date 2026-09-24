using Godot;
using System;

namespace Plinko;

public partial class GameOverOverlay : CanvasLayer
{
    public event Action RetryPressed;
    public event Action MenuPressed;

    private Control _root;
    private Label _palierLabel;
    private Label _recordLabel;
    private Label _chipsLabel;
    private GridContainer _stats;
    private Button _retry;

    public Button RetryButton => _retry;

    public override void _Ready()
    {
        Layer = 15;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _root = new Control();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var dim = new ColorRect { Color = new Color(0.03f, 0f, 0.03f, 0.84f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(540f, 0f) };
        var panelStyle = UiTheme.Box(Pal.Panel, Pal.Alpha(Pal.Red, 0.7f), 2, 18, 26);
        panelStyle.ShadowColor = Pal.Alpha(Pal.Red, 0.25f);
        panelStyle.ShadowSize = 24;
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        vbox.AddChild(Ui.Label("PARTIE TERMINÉE", 50, Pal.Red, Fonts.Black, HorizontalAlignment.Center, 8));
        _palierLabel = Ui.Label("", 22, Pal.Text, Fonts.Bold, HorizontalAlignment.Center);
        vbox.AddChild(_palierLabel);
        _recordLabel = Ui.Label("★  NOUVEAU RECORD  ★", 22, Pal.Gold, Fonts.Black, HorizontalAlignment.Center, 4);
        vbox.AddChild(_recordLabel);
        _chipsLabel = Ui.Label("", 20, Pal.Cyan, Fonts.Bold, HorizontalAlignment.Center, 4);
        vbox.AddChild(_chipsLabel);

        vbox.AddChild(new HSeparator());

        _stats = new GridContainer { Columns = 2 };
        _stats.AddThemeConstantOverride("h_separation", 30);
        _stats.AddThemeConstantOverride("v_separation", 6);
        var statsCenter = new CenterContainer();
        statsCenter.AddChild(_stats);
        vbox.AddChild(statsCenter);

        vbox.AddChild(new HSeparator());

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 14);
        vbox.AddChild(buttons);

        _retry = new Button { Text = "Rejouer", CustomMinimumSize = new Vector2(200f, 54f) };
        _retry.Pressed += () => { Sfx.Play(Sound.Click); RetryPressed?.Invoke(); };
        _retry.MouseEntered += () => Sfx.Play(Sound.Hover);
        buttons.AddChild(_retry);

        var menu = new Button { Text = "Menu / compétences", CustomMinimumSize = new Vector2(250f, 54f) };
        menu.Pressed += () => { Sfx.Play(Sound.Click); MenuPressed?.Invoke(); };
        menu.MouseEntered += () => Sfx.Play(Sound.Hover);
        buttons.AddChild(menu);
    }

    public void Display()
    {
        var run = RunManager.Instance;
        var record = run.Record;
        var palier = run.CurrentPalier;

        _palierLabel.Text = $"Palier {palier.Index + 1}  —  {Pal.FormatScore(run.Score)} / {Pal.FormatScore(palier.ScoreTarget)}";
        _recordLabel.Visible = SaveData.LastRunSetRecord;
        var reward = SkillTree.RunReward(record.PaliersCleared, record.BossesBeaten, run.Level);
        _chipsLabel.Text = $"+{reward.total} jetons   (paliers {reward.fromPaliers} · boss {reward.fromBosses} · niveau {reward.fromLevel})";

        foreach (Node child in _stats.GetChildren())
        {
            child.QueueFree();
        }
        Stat("Score total", Pal.FormatScore(record.TotalScore));
        Stat("Meilleur coup", record.BestHitPayout > 0 ? $"{Pal.FormatScore(record.BestHitPayout)}  ({Pal.FormatMultiplier(record.BestHitMultiplier)})" : "—");
        Stat("Jackpots (x10+)", $"{record.Jackpots}");
        Stat("Billes lâchées", $"{record.BallsDropped}");
        Stat("Clous touchés", $"{record.PegHits}");
        Stat("Niveau atteint", $"{run.Level}");
        Stat("Coffres / maudits", $"{record.ChestsOpened} / {record.CursedChestsOpened}");
        Stat("Cocktails", $"{run.ActiveModifiers.Count}");
        Stat("Record (palier)", $"{SaveData.BestPalier}");
        Stat("Jetons disponibles", $"{SaveData.Chips}");

        Visible = true;
        _root.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_root, "modulate:a", 1f, 0.4f);
        _retry.GrabFocus();
    }

    private void Stat(string label, string value)
    {
        _stats.AddChild(Ui.Label(label, 17, Pal.TextDim, Fonts.Regular));
        _stats.AddChild(Ui.Label(value, 17, Pal.Text, Fonts.Bold, HorizontalAlignment.Right));
    }
}
