using Godot;

namespace Plinko;

public partial class IdleTitleScreen : Node2D
{
    private const float Offset = 300f; // the 900px-wide title art, centred on a 1500px screen

    private Button _wipe;
    private bool _wipeArmed;
    private double _wipeTimer;

    public Button PlayButton { get; private set; }

    public override void _Ready()
    {
        var idle = IdleManager.Instance;
        AddChild(new CasinoBackground { ViewportSize = new Vector2(1500f, 1000f) });
        AddChild(new TitleArt { Position = new Vector2(Offset, 0f) });

        var layer = new CanvasLayer { Layer = 2 };
        AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var subtitle = Ui.Label("ÉDITION INCRÉMENTALE", 22, Pal.Cyan, Fonts.Bold, HorizontalAlignment.Center, 4);
        subtitle.Position = new Vector2(0f, 392f);
        subtitle.Size = new Vector2(1500f, 30f);
        root.AddChild(subtitle);

        var collection = Ui.Label($"TA COLLECTION DE CHAUSSURES  ·  {idle.UnlockedShoes}/{Characters.All.Count}", 18, Pal.Gold, Fonts.Bold, HorizontalAlignment.Center, 4);
        collection.Position = new Vector2(0f, 446f);
        collection.Size = new Vector2(1500f, 26f);
        root.AddChild(collection);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        row.Position = new Vector2(0f, 482f);
        row.Size = new Vector2(1500f, 200f);
        root.AddChild(row);
        for (int i = 0; i < Characters.All.Count; i++)
        {
            row.AddChild(new ShoeCollectionCard { Index = i });
        }

        bool hasProgress = idle.LifetimeEarned > 0 || idle.Prestiges > 0;
        PlayButton = new Button { Text = hasProgress ? "CONTINUER" : "JOUER", Position = new Vector2(620f, 730f), Size = new Vector2(260f, 66f) };
        PlayButton.AddThemeFontOverride("font", Fonts.Black);
        PlayButton.AddThemeFontSizeOverride("font_size", 32);
        var playStyle = UiTheme.Box(new Color(0.45f, 0.06f, 0.25f), Pal.Gold, 3, 16, 12);
        playStyle.ShadowColor = Pal.Alpha(Pal.Pink, 0.45f);
        playStyle.ShadowSize = 16;
        var playHover = (StyleBoxFlat)playStyle.Duplicate();
        playHover.BgColor = new Color(0.62f, 0.1f, 0.35f);
        PlayButton.AddThemeStyleboxOverride("normal", playStyle);
        PlayButton.AddThemeStyleboxOverride("hover", playHover);
        PlayButton.AddThemeStyleboxOverride("pressed", playHover);
        PlayButton.AddThemeStyleboxOverride("focus", playHover);
        PlayButton.MouseEntered += () => Sfx.Play(Sound.Hover);
        PlayButton.Pressed += Play;
        root.AddChild(PlayButton);

        string stats = hasProgress
            ? $"{Big.Format(idle.Coins)} pièces   ·   {idle.Jetons} jetons   ·   {idle.Prestiges} changement(s) de chaussures   ·   gains totaux {Big.Format(idle.LifetimeEarned)}"
            : "Lâche des billes, achète-en de meilleures, automatise tout… et collectionne les chaussures.";
        var statsLabel = Ui.Label(stats, 16, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center);
        statsLabel.Position = new Vector2(0f, 820f);
        statsLabel.Size = new Vector2(1500f, 24f);
        root.AddChild(statsLabel);

        _wipe = new Button { Text = "Effacer la sauvegarde", Position = new Vector2(640f, 880f), Size = new Vector2(220f, 40f), Visible = hasProgress };
        _wipe.AddThemeFontSizeOverride("font_size", 15);
        _wipe.Pressed += OnWipe;
        root.AddChild(_wipe);

        var hint = Ui.Label("Entrée : jouer   ·   Échap : quitter", 14, Pal.Alpha(Pal.TextDim, 0.6f), Fonts.Regular, HorizontalAlignment.Center);
        hint.Position = new Vector2(0f, 950f);
        hint.Size = new Vector2(1500f, 22f);
        root.AddChild(hint);
    }

    private void Play()
    {
        Sfx.Play(Sound.Pick);
        Main.Instance.StartGame();
    }

    private void OnWipe()
    {
        if (!_wipeArmed)
        {
            _wipeArmed = true;
            _wipeTimer = 3.0;
            _wipe.Text = "Vraiment tout effacer ?";
            Sfx.Play(Sound.Click);
            return;
        }
        IdleManager.Instance.WipeSave();
        Sfx.Play(Sound.Malus);
        Main.Instance.ShowTitle();
    }

    public override void _Process(double delta)
    {
        if (_wipeArmed)
        {
            _wipeTimer -= delta;
            if (_wipeTimer <= 0)
            {
                _wipeArmed = false;
                _wipe.Text = "Effacer la sauvegarde";
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space) Play();
        else if (key.Keycode == Key.Escape) GetTree().Quit();
        else return;
        GetViewport().SetInputAsHandled();
    }
}

public partial class ShoeCollectionCard : Control
{
    public int Index;
    private float _time;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(170f, 200f);
        var shoe = Characters.All[Index];
        bool unlocked = IdleManager.Instance.IsShoeUnlocked(Index);
        AddChild(Ui.Wrapped(unlocked ? shoe.Name : "???", 17, unlocked ? Pal.Text : Pal.TextDim, Fonts.Bold, HorizontalAlignment.Center,
            new Vector2(6f, 128f), new Vector2(158f, 26f), 3));
        AddChild(Ui.Wrapped(unlocked ? $"jetons x{shoe.JetonMultiplier:0}" : $"Gagne {Big.Format(shoe.UnlockRunEarnings)}", 13, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center,
            new Vector2(6f, 156f), new Vector2(158f, 40f)));
        TooltipText = $"{shoe.Name} : {shoe.DifficultyText}";
        _time = GD.Randf() * 4f;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var idle = IdleManager.Instance;
        var shoe = Characters.All[Index];
        bool unlocked = idle.IsShoeUnlocked(Index);
        bool worn = idle.ShoeId == shoe.Id;
        var rect = new Rect2(Vector2.Zero, Size);
        var accent = worn ? Pal.Gold : unlocked ? shoe.ShoeColor.Lightened(0.2f) : new Color(0.35f, 0.32f, 0.4f);

        if (worn)
        {
            DrawColoredPolygon(Paint.RoundedRect(rect.Grow(6f + 2f * Mathf.Sin(_time * 3f)), 20f), Pal.Alpha(Pal.Gold, 0.18f));
        }
        DrawColoredPolygon(Paint.RoundedRect(rect, 16f), new Color(0.1f, 0.05f, 0.13f, 0.95f));
        DrawPolyline(Paint.Closed(Paint.RoundedRect(rect, 16f)), accent, worn ? 3f : 2f, true);

        var center = new Vector2(Size.X / 2f, 84f);
        Paint.Halo(this, center + new Vector2(0f, -20f), 60f, Pal.Alpha(accent, unlocked ? 0.25f : 0.08f), 5);
        float scale = shoe.Style == ShoeStyle.Boot ? 0.62f : 0.7f;
        ShoeArt.DrawShoeOnly(this, shoe, new Vector2(Size.X / 2f - 58f, center.Y + 20f), scale, false);
        if (!unlocked)
        {
            DrawColoredPolygon(Paint.RoundedRect(new Rect2(10f, 10f, Size.X - 20f, 110f), 12f), new Color(0.02f, 0.01f, 0.04f, 0.78f));
            DrawRect(new Rect2(center + new Vector2(-12f, -8f), new Vector2(24f, 20f)), Pal.TextDim);
            DrawArc(center + new Vector2(0f, -10f), 9f, Mathf.Pi, Mathf.Tau, 14, Pal.TextDim, 3f);
        }
    }
}

public partial class TitleArt : Node2D
{
    private float _time;
    private float _flicker = 1f;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _flicker = GD.Randf() < 0.01f ? 0.4f : Mathf.Min(1f, _flicker + (float)delta * 5f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var frame = new Rect2(120f, 60f, 660f, 300f);
        DrawColoredPolygon(Paint.RoundedRect(frame.Grow(10f), 36f), new Color(0f, 0f, 0f, 0.35f));
        DrawColoredPolygon(Paint.RoundedRect(frame, 30f), new Color(0.07f, 0.02f, 0.08f, 0.92f));
        DrawPolyline(Paint.Closed(Paint.RoundedRect(frame, 30f)), Pal.Hdr(Pal.Gold, 1.1f), 3f, true);

        int phase = (int)(_time * 8f);
        int bulbs = 0;
        for (float x = frame.Position.X + 30f; x <= frame.End.X - 30f; x += 26f)
        {
            Bulb(new Vector2(x, frame.Position.Y + 14f), bulbs++, phase);
            Bulb(new Vector2(x, frame.End.Y - 14f), bulbs++, phase);
        }
        for (float y = frame.Position.Y + 40f; y <= frame.End.Y - 40f; y += 26f)
        {
            Bulb(new Vector2(frame.Position.X + 14f, y), bulbs++, phase);
            Bulb(new Vector2(frame.End.X - 14f, y), bulbs++, phase);
        }

        float glow = _flicker;
        var title = new Vector2(450f, 186f);
        Paint.Halo(this, title, 260f, Pal.Alpha(Pal.Pink, 0.1f * glow), 6);
        Paint.TextCentered(this, Fonts.Black, title + new Vector2(0f, 6f), "PLINKO", 150, new Color(0.25f, 0.02f, 0.12f), 10, new Color(0.12f, 0.01f, 0.06f));
        Paint.TextCentered(this, Fonts.Black, title, "PLINKO", 150, Pal.Hdr(new Color(1f, 0.62f, 0.84f), 1.05f + 0.2f * glow), 5, Pal.Hdr(Pal.Pink, 0.95f * glow));

        var palace = new Vector2(450f, 302f);
        Paint.TextCentered(this, Fonts.Bold, palace, "P  A  L  A  C  E", 36, Pal.Hdr(Pal.Gold, 1.15f), 3, new Color(0.3f, 0.15f, 0.02f));
        DrawLine(palace + new Vector2(-250f, 0f), palace + new Vector2(-170f, 0f), Pal.Hdr(Pal.Gold, 1.2f), 2f);
        DrawLine(palace + new Vector2(170f, 0f), palace + new Vector2(250f, 0f), Pal.Hdr(Pal.Gold, 1.2f), 2f);

        // A rain of coloured balls on both sides, one per tier.
        for (int i = 0; i < 12; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            var def = BallTiers.All[i % BallTiers.All.Length];
            float t = _time * (0.8f + 0.1f * (i % 4)) + i * 0.83f;
            var p = new Vector2(450f + side * (390f + 60f * (i / 2 % 3) + Mathf.Sin(t * 2f) * 10f), -20f + Mathf.PosMod(t * 110f, 1040f));
            Paint.Halo(this, p, 22f, Pal.Alpha(def.Glow, 0.4f));
            DrawCircle(p, 8f, Pal.Hdr(def.Color, 1.2f));
        }
    }

    private void Bulb(Vector2 p, int index, int phase)
    {
        bool lit = (index + phase) % 4 < 2;
        if (lit) DrawCircle(p, 7f, Pal.Alpha(Pal.Gold, 0.2f));
        DrawCircle(p, 3f, lit ? Pal.Hdr(Pal.Gold, 2.2f) : new Color(0.35f, 0.24f, 0.12f));
    }
}
