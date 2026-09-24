using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

public partial class PauseMenu : CanvasLayer
{
    public event Action ResumePressed;
    public event Action RestartPressed;
    public event Action MenuPressed;

    private Control _root;
    private VBoxContainer _buildList;

    public Button ResumeButton { get; private set; }

    public override void _Ready()
    {
        Layer = 12;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _root = new Control();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var dim = new ColorRect { Color = new Color(0.02f, 0f, 0.04f, 0.8f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560f, 0f) };
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(vbox);

        vbox.AddChild(Ui.Label("PAUSE", 48, Pal.Cyan, Fonts.Black, HorizontalAlignment.Center, 6));

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(520f, 330f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        vbox.AddChild(scroll);
        _buildList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _buildList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_buildList);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(buttons);
        ResumeButton = MakeButton("Reprendre", () => ResumePressed?.Invoke());
        buttons.AddChild(ResumeButton);
        buttons.AddChild(MakeButton("Recommencer", () => RestartPressed?.Invoke()));
        buttons.AddChild(MakeButton("Menu", () => MenuPressed?.Invoke()));
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(150f, 48f) };
        button.MouseEntered += () => Sfx.Play(Sound.Hover);
        button.Pressed += () =>
        {
            Sfx.Play(Sound.Click);
            action();
        };
        return button;
    }

    public void Open()
    {
        RebuildSummary();
        Visible = true;
        _root.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_root, "modulate:a", 1f, 0.15f);
    }

    public void Close() => Visible = false;

    // The game is paused while this menu is open, so the game screen can't hear Escape.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Escape or Key.P)
        {
            GetViewport().SetInputAsHandled();
            ResumePressed?.Invoke();
        }
    }

    private void RebuildSummary()
    {
        foreach (Node child in _buildList.GetChildren())
        {
            child.QueueFree();
        }

        var run = RunManager.Instance;
        var s = run.Stats;

        Section($"{run.SelectedCharacter.Name}", Pal.Gold);
        Line(run.SelectedCharacter.Perk.Replace("\n", " "), Pal.TextDim);

        Section("Tes statistiques", Pal.Cyan);
        foreach (var (label, value) in StatLines(s))
        {
            Row(label, value);
        }

        Section("Cocktails", Pal.Pink);
        if (run.ActiveModifiers.Count == 0)
        {
            Line("Aucun pour l'instant — ouvre des coffres épiques !", Pal.TextDim);
        }
        foreach (var cocktail in run.ActiveModifiers)
        {
            Row(cocktail.DisplayName, cocktail.Description, cocktail.LiquidColor);
        }

        Section("Coffres ouverts", Pal.Green);
        if (run.Record.UpgradesInOrder.Count == 0)
        {
            Line("Aucun — touche les coffres avec tes billes.", Pal.TextDim);
        }
        foreach (var (id, count) in run.Record.UpgradeCounts)
        {
            var option = Array.Find(UpgradeCatalog.All, o => o.Id == id) ?? Array.Find(MalusCatalog.All, o => o.Id == id);
            if (option != null)
            {
                Row(option.Label, count > 1 ? $"x{count}" : "", Pal.ForRarity(option.Rarity));
            }
        }
    }

    public static IEnumerable<(string, string)> StatLines(RunStats s)
    {
        yield return ("Rangées de clous", $"{s.RowCount}");
        yield return ("Multiplicateur de gains", $"x{s.GlobalMultiplierModifier:0.00}".Replace(',', '.'));
        yield return ("Billes bonus par palier", s.BonusBallsPerPalier >= 0 ? $"+{s.BonusBallsPerPalier}" : $"{s.BonusBallsPerPalier}");
        if (s.ExtraFreeBalls > 0) yield return ("Billes jumelles", $"+{s.ExtraFreeBalls}");
        yield return ("Taille des billes", $"{s.BallRadius / 9f * 100f:0}%");
        yield return ("Bouclier (mauvaises cases)", $"+{s.NegativePenaltyReduction * 100f:0}%");
        if (s.GoldenBallChance > 0f) yield return ("Chance de bille dorée", $"{s.GoldenBallChance * 100f:0}% (x{s.GoldenMultiplier:0})");
        if (s.PegHitScore > 0f) yield return ("Points par clou", $"+{s.PegHitScore:0.##}".Replace(',', '.'));
        if (s.PlacedBlockerCount > 0) yield return ("Bâtons placés", $"{s.PlacedBlockerCount}");
        if (s.BlockerCount > 0) yield return ("Bâtons sauvages", $"{s.BlockerCount}");
        if (s.PortalCount > 0) yield return ("Portails dédoubleurs", $"{s.PortalCount}");
        if (s.SlotWidthModifier < 0.999f) yield return ("Largeur des cases", $"{s.SlotWidthModifier * 100f:0}%");
        if (s.JackpotBonus > 0f) yield return ("Bonus jackpots", $"+{s.JackpotBonus * 100f:0}%");
        if (s.Rerolls > 0) yield return ("Relances de coffre", $"{s.Rerolls}");
        if (s.ChestChoices > 3) yield return ("Cartes par coffre", $"{s.ChestChoices}");
        if (s.XpMultiplier > 1.001f) yield return ("Bonus d'expérience", $"+{(s.XpMultiplier - 1f) * 100f:0}%");
    }

    private void Section(string text, Color color)
    {
        var label = Ui.Label(text.ToUpperInvariant(), 17, color, Fonts.Bold);
        label.CustomMinimumSize = new Vector2(0f, 30f);
        _buildList.AddChild(label);
    }

    private void Line(string text, Color color)
    {
        var label = Ui.Label(text, 15, color, Fonts.Regular);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(500f, 0f);
        _buildList.AddChild(label);
    }

    private void Row(string left, string right, Color? leftColor = null)
    {
        var row = new HBoxContainer();
        var l = Ui.Label(left, 15, leftColor ?? Pal.Text, Fonts.Bold);
        l.CustomMinimumSize = new Vector2(190f, 0f);
        row.AddChild(l);
        var r = Ui.Label(right, 15, Pal.TextDim, Fonts.Regular);
        r.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        r.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        r.CustomMinimumSize = new Vector2(300f, 0f);
        row.AddChild(r);
        _buildList.AddChild(row);
    }
}
