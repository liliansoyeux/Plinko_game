using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// Right-hand shop: balls, upgrades, shoes (prestige) and the skill tree entry point.
public partial class ShopPanel : CanvasLayer
{
    public const float PanelX = 900f;
    public const float PanelWidth = 600f;

    public event Action OpenSkillTree;
    public event Action<CharacterDef> PrestigeRequested;

    private enum Tab { Balls, Upgrades, Shoes, Skills }

    private Tab _tab = Tab.Balls;
    private Label _coins;
    private Label _income;
    private Label _jetons;
    private VBoxContainer _list;
    private ScrollContainer _scroll;
    private readonly List<Button> _tabButtons = new();
    private readonly List<IShopRow> _rows = new();
    private double _refreshTimer;

    // Buy amount for balls: 1, 10, 100 or -1 for "max".
    public int BuyAmount { get; private set; } = 1;

    public override void _Ready()
    {
        Layer = 4;
        var root = new ShopBackground { Position = new Vector2(PanelX, 0f), Size = new Vector2(PanelWidth, 1000f), MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(root);

        root.AddChild(new CoinIcon { Position = new Vector2(24f, 26f), Size = new Vector2(48f, 48f) });
        _coins = Ui.Label("0", 44, Pal.Gold, Fonts.Black, HorizontalAlignment.Left, 6);
        _coins.Position = new Vector2(82f, 16f);
        _coins.Size = new Vector2(360f, 60f);
        root.AddChild(_coins);

        _income = Ui.Label("", 20, Pal.Cyan, Fonts.Bold, HorizontalAlignment.Left, 3);
        _income.Position = new Vector2(84f, 76f);
        _income.Size = new Vector2(300f, 28f);
        root.AddChild(_income);

        _jetons = Ui.Label("", 18, Pal.Purple.Lightened(0.3f), Fonts.Bold, HorizontalAlignment.Right, 3);
        _jetons.Position = new Vector2(330f, 30f);
        _jetons.Size = new Vector2(250f, 60f);
        _jetons.AutowrapMode = TextServer.AutowrapMode.Off;
        root.AddChild(_jetons);

        var tabs = new HBoxContainer { Position = new Vector2(16f, 118f), Size = new Vector2(568f, 48f) };
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        foreach (var (tab, label) in new[] { (Tab.Balls, "Billes"), (Tab.Upgrades, "Améliorations"), (Tab.Shoes, "Chaussures"), (Tab.Skills, "Compétences") })
        {
            var button = new Button { Text = label, CustomMinimumSize = new Vector2(0f, 44f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None };
            button.AddThemeFontSizeOverride("font_size", 16);
            button.Pressed += () => { Sfx.Play(Sound.Click); SelectTab(tab); };
            tabs.AddChild(button);
            _tabButtons.Add(button);
        }

        _scroll = new ScrollContainer
        {
            Position = new Vector2(12f, 178f),
            Size = new Vector2(576f, 810f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(_scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        _scroll.AddChild(_list);

        IdleManager.Instance.Changed += RequestRebuildIfNeeded;
        SelectTab(Tab.Balls);
    }

    public override void _ExitTree()
    {
        IdleManager.Instance.Changed -= RequestRebuildIfNeeded;
    }

    private int _visibleTiers;
    private int _unlockedShoesShown;

    // New ball tiers / shoes appearing need a rebuild; everything else is a cheap refresh.
    private void RequestRebuildIfNeeded()
    {
        if (_tab == Tab.Balls && VisibleTierCount() != _visibleTiers) Rebuild();
        if (_tab == Tab.Shoes && IdleManager.Instance.UnlockedShoes != _unlockedShoesShown) Rebuild();
    }

    private static int VisibleTierCount()
    {
        var owned = IdleManager.Instance.BallsOwned;
        int highest = 0;
        for (int t = 0; t < owned.Length; t++)
        {
            if (owned[t] > 0) highest = t;
        }
        return Math.Min(BallTiers.All.Length, highest + 2);
    }

    public void SelectTabIndex(int index) => SelectTab((Tab)index);

    private void SelectTab(Tab tab)
    {
        _tab = tab;
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].AddThemeColorOverride("font_color", i == (int)tab ? Pal.Gold : Pal.TextDim);
            _tabButtons[i].Modulate = i == (int)tab ? Colors.White : new Color(1f, 1f, 1f, 0.8f);
        }
        Rebuild();
        _scroll.ScrollVertical = 0;
    }

    private void Rebuild()
    {
        foreach (Node child in _list.GetChildren())
        {
            child.QueueFree();
        }
        _rows.Clear();
        var idle = IdleManager.Instance;

        switch (_tab)
        {
            case Tab.Balls:
                _list.AddChild(BuildAmountBar());
                if (idle.SkillLevel("a_balls") > 0)
                {
                    _list.AddChild(AutoToggle("Achat automatique des billes (Majordome)", idle.AutoBuyBalls, v => idle.AutoBuyBalls = v));
                }
                _visibleTiers = VisibleTierCount();
                for (int t = 0; t < _visibleTiers; t++)
                {
                    Add(new BallRow { Tier = t, Panel = this });
                }
                if (_visibleTiers < BallTiers.All.Length)
                {
                    _list.AddChild(Note("Achète une bille du dernier type pour découvrir la suivante."));
                }
                break;

            case Tab.Upgrades:
                if (idle.SkillLevel("a_upgrades") > 0)
                {
                    _list.AddChild(AutoToggle("Achat automatique des améliorations (Intendant)", idle.AutoBuyUpgrades, v => idle.AutoBuyUpgrades = v));
                }
                foreach (var def in Upgrades.All)
                {
                    Add(new UpgradeRow { Def = def });
                }
                break;

            case Tab.Shoes:
                _unlockedShoesShown = idle.UnlockedShoes;
                _list.AddChild(new PrestigeSummary());
                for (int i = 0; i < Characters.All.Count; i++)
                {
                    var row = new ShoeRow { Index = i };
                    row.PrestigeRequested += shoe => PrestigeRequested?.Invoke(shoe);
                    Add(row);
                }
                break;

            case Tab.Skills:
                _list.AddChild(Note($"Les jetons s'obtiennent en changeant de chaussures (onglet Chaussures). Chaque jeton gagné donne aussi +1% de revenus pour toujours : actuellement x{1.0 + 0.01 * idle.JetonsEarnedTotal:0.00}.".Replace(',', '.')));
                var open = new Button { Text = "Ouvrir l'arbre de compétences", CustomMinimumSize = new Vector2(0f, 64f) };
                open.AddThemeFontSizeOverride("font_size", 22);
                open.Pressed += () => { Sfx.Play(Sound.Click); OpenSkillTree?.Invoke(); };
                _list.AddChild(open);
                _list.AddChild(new StatsBlock());
                break;
        }
        Refresh();
    }

    private void Add(Control row)
    {
        _list.AddChild(row);
        if (row is IShopRow r) _rows.Add(r);
    }

    private Control BuildAmountBar()
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 6);
        bar.AddChild(Ui.Label("Acheter :", 16, Pal.TextDim, Fonts.Bold));
        foreach (int amount in new[] { 1, 10, 100, -1 })
        {
            var button = new Button { Text = amount < 0 ? "MAX" : $"x{amount}", CustomMinimumSize = new Vector2(80f, 38f), FocusMode = Control.FocusModeEnum.None };
            button.AddThemeFontSizeOverride("font_size", 16);
            if (amount == BuyAmount) button.AddThemeColorOverride("font_color", Pal.Gold);
            int captured = amount;
            button.Pressed += () => { BuyAmount = captured; Sfx.Play(Sound.Click); Rebuild(); };
            bar.AddChild(button);
        }
        return bar;
    }

    private static Control AutoToggle(string text, bool value, Action<bool> set)
    {
        var check = new CheckButton { Text = text, ButtonPressed = value, FocusMode = Control.FocusModeEnum.None };
        check.AddThemeColorOverride("font_color", Pal.Green);
        check.Toggled += v => { set(v); Sfx.Play(Sound.Click); };
        return check;
    }

    private static Label Note(string text)
    {
        var label = Ui.Label(text, 15, Pal.TextDim, Fonts.Regular);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(560f, 0f);
        return label;
    }

    public override void _Process(double delta)
    {
        _refreshTimer -= delta;
        if (_refreshTimer <= 0)
        {
            _refreshTimer = 0.15;
            Refresh();
        }
    }

    private void Refresh()
    {
        var idle = IdleManager.Instance;
        _coins.Text = Big.Format(idle.Coins);
        _income.Text = $"+{Big.Format(idle.IncomePerSecond)} par seconde";
        _jetons.Text = $"{idle.Jetons} jetons\n{idle.Shoe.Name}";
        foreach (var row in _rows)
        {
            row.Refresh();
        }
    }
}

public interface IShopRow
{
    void Refresh();
}

public partial class ShopBackground : Control
{
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        Paint.VerticalGradient(this, rect, new Color(0.09f, 0.04f, 0.12f, 0.98f), new Color(0.04f, 0.02f, 0.06f, 0.98f));
        DrawLine(new Vector2(1f, 0f), new Vector2(1f, Size.Y), Pal.Hdr(Pal.Gold, 1.1f), 3f);
        DrawLine(new Vector2(6f, 0f), new Vector2(6f, Size.Y), Pal.Alpha(Pal.Gold, 0.25f), 1f);
        DrawLine(new Vector2(16f, 108f), new Vector2(Size.X - 16f, 108f), Pal.Alpha(Pal.Gold, 0.3f), 1f);
    }
}

public partial class CoinIcon : Control
{
    public override void _Draw()
    {
        var c = Size / 2f;
        float r = Size.X / 2f;
        DrawCircle(c, r, Pal.Gold.Darkened(0.35f));
        DrawCircle(c, r * 0.86f, Pal.Hdr(Pal.Gold, 1.05f));
        DrawArc(c, r * 0.66f, 0f, Mathf.Tau, 32, Pal.Gold.Darkened(0.25f), 2f, true);
        Paint.TextCentered(this, Fonts.Black, c, "P", (int)(r * 1.1f), Pal.Gold.Darkened(0.4f));
    }
}

// Common row chrome: rounded card with an icon well on the left and a buy button on the right.
public abstract partial class ShopRowBase : Control, IShopRow
{
    protected Label Title;
    protected Label Info;
    protected Button Buy;
    protected Color Accent = Pal.Pink;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(560f, 96f);
        Title = Ui.Label("", 19, Pal.Text, Fonts.Bold);
        Title.Position = new Vector2(92f, 10f);
        Title.Size = new Vector2(280f, 26f);
        AddChild(Title);
        Info = Ui.Wrapped("", 13, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Left, new Vector2(92f, 38f), new Vector2(284f, 54f));
        AddChild(Info);
        Buy = new Button { Position = new Vector2(386f, 14f), Size = new Vector2(164f, 68f), FocusMode = Control.FocusModeEnum.None };
        Buy.AddThemeFontSizeOverride("font_size", 16);
        // Affordable = green and inviting; too expensive = muted but still readable.
        var ready = UiTheme.Box(new Color(0.08f, 0.28f, 0.16f), Pal.Green, 2, 10, 6);
        var readyHover = UiTheme.Box(new Color(0.12f, 0.4f, 0.22f), Pal.Green.Lightened(0.3f), 2, 10, 6);
        Buy.AddThemeStyleboxOverride("normal", ready);
        Buy.AddThemeStyleboxOverride("hover", readyHover);
        Buy.AddThemeStyleboxOverride("pressed", readyHover);
        Buy.AddThemeStyleboxOverride("disabled", UiTheme.Box(new Color(0.1f, 0.07f, 0.13f), new Color(0.35f, 0.3f, 0.4f), 1, 10, 6));
        Buy.AddThemeColorOverride("font_disabled_color", new Color(0.66f, 0.6f, 0.72f));
        Buy.Pressed += OnBuy;
        AddChild(Buy);
    }

    protected abstract void OnBuy();
    public abstract void Refresh();
    protected abstract void DrawIcon(Vector2 center);

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawColoredPolygon(Paint.RoundedRect(rect, 12f), new Color(0.12f, 0.06f, 0.17f, 0.95f));
        DrawPolyline(Paint.Closed(Paint.RoundedRect(rect, 12f)), Pal.Alpha(Accent, 0.45f), 1.5f, true);
        var iconCenter = new Vector2(46f, Size.Y / 2f);
        DrawCircle(iconCenter, 32f, Accent.Darkened(0.75f));
        DrawArc(iconCenter, 32f, 0f, Mathf.Tau, 36, Pal.Alpha(Accent, 0.8f), 2f, true);
        DrawIcon(iconCenter);
    }
}

public partial class BallRow : ShopRowBase
{
    public int Tier;
    public ShopPanel Panel;
    private float _time;

    public override void _Ready()
    {
        base._Ready();
        Accent = BallTiers.All[Tier].Glow;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    private int Amount()
    {
        var idle = IdleManager.Instance;
        return Panel.BuyAmount < 0 ? Math.Max(1, idle.MaxAffordableBalls(Tier)) : Panel.BuyAmount;
    }

    protected override void OnBuy()
    {
        if (IdleManager.Instance.BuyBalls(Tier, Amount()))
        {
            Sfx.Play(Sound.Pick, 0.9f + Tier * 0.08f, -4f);
        }
        else
        {
            Sfx.Play(Sound.Click, 0.6f);
        }
    }

    public override void Refresh()
    {
        var idle = IdleManager.Instance;
        var def = BallTiers.All[Tier];
        int owned = idle.BallsOwned[Tier];
        int amount = Amount();
        double cost = idle.BallCost(Tier, amount);
        double each = def.Value * idle.GlobalMultiplier;
        Title.Text = $"{def.Name}  ·  {Big.Format(owned)}";
        string stack = owned > IdleBoard.TokenCap ? $" (chaque bille en jeu en vaut {owned / (double)IdleBoard.TokenCap:0.#})".Replace(',', '.') : "";
        Info.Text = $"Vaut {Big.Format(each)} x la case où elle tombe.{stack}";
        Buy.Text = $"Acheter x{amount}\n{Big.Format(cost)}";
        Buy.Disabled = cost > idle.Coins;
    }

    protected override void DrawIcon(Vector2 center)
    {
        var def = BallTiers.All[Tier];
        var glow = Tier == 5 ? Pal.Prismatic(_time) : def.Glow;
        Paint.Halo(this, center, 30f, Pal.Alpha(glow, 0.5f));
        DrawCircle(center, 15f, Pal.Hdr(def.Color, 1.1f));
        DrawCircle(center + new Vector2(0f, 3f), 12f, Pal.Alpha(def.Color.Darkened(0.35f), 0.45f));
        DrawCircle(center + new Vector2(-5f, -5f), 5f, Pal.Hdr(Colors.White, 1.4f));
    }
}

public partial class UpgradeRow : ShopRowBase
{
    public UpgradeDef Def;

    public override void _Ready()
    {
        base._Ready();
        Accent = Def.Id == IdleUpgrade.Portal ? Pal.Legendary : Pal.Cyan;
    }

    protected override void OnBuy()
    {
        if (IdleManager.Instance.BuyUpgrade(Def.Id))
        {
            Sfx.Play(Sound.Pick);
        }
        else
        {
            Sfx.Play(Sound.Click, 0.6f);
        }
    }

    public override void Refresh()
    {
        var idle = IdleManager.Instance;
        int level = idle.Level(Def.Id);
        bool maxed = idle.IsMaxed(Def.Id);
        Title.Text = Def.MaxLevel == 1 ? Def.Name : $"{Def.Name}  ·  {level}/{Def.MaxLevel}";
        Info.Text = maxed ? "Niveau maximum atteint." : Def.Describe(level);
        if (maxed)
        {
            Buy.Text = "MAX";
            Buy.Disabled = true;
            return;
        }
        double cost = idle.UpgradeCost(Def.Id);
        Buy.Text = $"Acheter\n{Big.Format(cost)}";
        Buy.Disabled = cost > idle.Coins;
    }

    protected override void DrawIcon(Vector2 center) => UpgradeIcons.Draw(this, center, 30f, Def.Icon, Accent);
}

public partial class PrestigeSummary : Control, IShopRow
{
    private Label _text;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(560f, 118f);
        _text = Ui.Wrapped("", 15, Pal.Text, Fonts.Regular, HorizontalAlignment.Left, new Vector2(16f, 10f), new Vector2(530f, 100f));
        AddChild(_text);
        Refresh();
    }

    public void Refresh()
    {
        var idle = IdleManager.Instance;
        _text.Text =
            $"Gains de cette partie : {Big.Format(idle.RunEarned)}\n" +
            $"Changer de chaussures maintenant rapporte {idle.JetonsForPrestige} jeton(s)  (prochain à {Big.Format(idle.NextJetonAt)}).\n" +
            "Tu repars de zéro (pièces, billes, améliorations) mais tu gardes tes jetons, tes compétences et tes paires débloquées.";
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawColoredPolygon(Paint.RoundedRect(rect, 12f), new Color(0.2f, 0.08f, 0.3f, 0.6f));
        DrawPolyline(Paint.Closed(Paint.RoundedRect(rect, 12f)), Pal.Alpha(Pal.Purple, 0.8f), 2f, true);
    }
}

public partial class ShoeRow : ShopRowBase
{
    public int Index;
    public event Action<CharacterDef> PrestigeRequested;

    private bool _armed;
    private double _armedTimer;

    private CharacterDef Shoe => Characters.All[Index];

    public override void _Ready()
    {
        base._Ready();
        CustomMinimumSize = new Vector2(560f, 104f);
        Buy.Position = new Vector2(386f, 18f);
        Info.Size = new Vector2(284f, 60f);
        Accent = Shoe.ShoeColor.Lightened(0.2f);
    }

    public override void _Process(double delta)
    {
        if (_armed)
        {
            _armedTimer -= delta;
            if (_armedTimer <= 0) _armed = false;
        }
    }

    protected override void OnBuy()
    {
        if (!_armed)
        {
            _armed = true;
            _armedTimer = 3.0;
            Sfx.Play(Sound.Click);
            Refresh();
            return;
        }
        _armed = false;
        PrestigeRequested?.Invoke(Shoe);
    }

    public override void Refresh()
    {
        var idle = IdleManager.Instance;
        bool unlocked = idle.IsShoeUnlocked(Index);
        bool worn = idle.ShoeId == Shoe.Id;
        Title.Text = unlocked ? Shoe.Name : $"{Shoe.Name}  (verrouillées)";
        if (unlocked)
        {
            Info.Text = $"{Shoe.DifficultyText}\nJetons x{Shoe.JetonMultiplier:0}" + (worn ? "   ·   tu les portes" : "");
            Buy.Disabled = idle.JetonsForPrestige < 1;
            Buy.Text = _armed ? $"Confirmer ?\n+{idle.JetonsForPrestige} jetons" : "Recommencer\navec elles";
        }
        else
        {
            var previous = Characters.All[Index - 1];
            Info.Text = $"Débloquage : {Big.Format(Shoe.UnlockRunEarnings)} en une partie ({previous.Name} ou mieux).\n{Shoe.DifficultyText} · jetons x{Shoe.JetonMultiplier:0}";
            Buy.Disabled = true;
            Buy.Text = "Verrouillées";
        }
        QueueRedraw();
    }

    protected override void DrawIcon(Vector2 center)
    {
        bool unlocked = IdleManager.Instance.IsShoeUnlocked(Index);
        float scale = Shoe.Style == ShoeStyle.Boot ? 0.3f : 0.34f;
        ShoeArt.DrawShoeOnly(this, Shoe, center + new Vector2(-28f, 16f), scale, false);
        if (!unlocked)
        {
            DrawCircle(center, 31f, new Color(0f, 0f, 0f, 0.6f));
            DrawRect(new Rect2(center + new Vector2(-8f, -2f), new Vector2(16f, 13f)), Pal.TextDim);
            DrawArc(center + new Vector2(0f, -3f), 6f, Mathf.Pi, Mathf.Tau, 12, Pal.TextDim, 2.5f);
        }
    }
}

public partial class StatsBlock : Control
{
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(560f, 200f);
        var idle = IdleManager.Instance;
        AddChild(Ui.Wrapped(
            $"Statistiques\n" +
            $"Gains totaux : {Big.Format(idle.LifetimeEarned)}\n" +
            $"Changements de chaussures : {idle.Prestiges}\n" +
            $"Jetons gagnés au total : {idle.JetonsEarnedTotal}\n" +
            $"Paires débloquées : {idle.UnlockedShoes} / {Characters.All.Count}  (+{(idle.UnlockedShoes - 1) * 50}% de revenus)",
            16, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Left, new Vector2(8f, 12f), new Vector2(540f, 180f)));
    }
}
