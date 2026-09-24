using Godot;
using System.Collections.Generic;

namespace Plinko;

// Title + character select: pick a pair of shoes, press Play.
public partial class TitleScreen : Node2D
{
    private readonly List<CharacterCard> _cards = new();
    private int _selected;
    private Button _play;
    private Label _perk;

    public CharacterDef Selected => Characters.All[_selected];
    public Button PlayButton => _play;

    public override void _Ready()
    {
        AddChild(new CasinoBackground());
        AddChild(new TitleArt());

        _selected = Mathf.Max(0, Characters.All.FindIndex(c => c.Id == SaveData.LastCharacter));

        var layer = new CanvasLayer { Layer = 2 };
        AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var choose = Ui.Label("CHOISIS TES CHAUSSURES", 22, Pal.Gold, Fonts.Bold, HorizontalAlignment.Center, 4);
        choose.Position = new Vector2(0f, 420f);
        choose.Size = new Vector2(900f, 30f);
        root.AddChild(choose);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        row.Position = new Vector2(0f, 462f);
        row.Size = new Vector2(900f, 250f);
        root.AddChild(row);

        for (int i = 0; i < Characters.All.Count; i++)
        {
            int index = i;
            var card = new CharacterCard { Character = Characters.All[i] };
            card.Clicked += () => Select(index);
            card.DoubleClicked += () => { Select(index); Play(); };
            row.AddChild(card);
            _cards.Add(card);
        }

        _perk = Ui.Wrapped("", 19, Pal.Text, Fonts.Regular, HorizontalAlignment.Center,
            new Vector2(60f, 726f), new Vector2(780f, 50f), 4);
        root.AddChild(_perk);

        _play = new Button { Text = "JOUER", CustomMinimumSize = new Vector2(260f, 66f) };
        _play.AddThemeFontOverride("font", Fonts.Black);
        _play.AddThemeFontSizeOverride("font_size", 32);
        var playNormal = UiTheme.Box(new Color(0.45f, 0.06f, 0.25f), Pal.Gold, 3, 16, 12);
        playNormal.ShadowColor = Pal.Alpha(Pal.Pink, 0.45f);
        playNormal.ShadowSize = 16;
        var playHover = (StyleBoxFlat)playNormal.Duplicate();
        playHover.BgColor = new Color(0.62f, 0.1f, 0.35f);
        playHover.ShadowSize = 26;
        _play.AddThemeStyleboxOverride("normal", playNormal);
        _play.AddThemeStyleboxOverride("hover", playHover);
        _play.AddThemeStyleboxOverride("pressed", playHover);
        _play.AddThemeStyleboxOverride("focus", playHover);
        _play.Position = new Vector2(180f, 792f);
        _play.Size = new Vector2(260f, 66f);
        _play.MouseEntered += () => Sfx.Play(Sound.Hover);
        _play.Pressed += Play;
        root.AddChild(_play);

        var skills = new Button { Text = $"COMPÉTENCES  ·  {SaveData.Chips} jetons", Position = new Vector2(460f, 792f), Size = new Vector2(260f, 66f) };
        skills.AddThemeFontSizeOverride("font_size", 16);
        skills.MouseEntered += () => Sfx.Play(Sound.Hover);
        skills.Pressed += OpenSkills;
        root.AddChild(skills);

        string records = SaveData.BestPalier > 0
            ? $"Record : palier {SaveData.BestPalier}   ·   meilleur score {Pal.FormatScore(SaveData.BestScore)}   ·   {SaveData.RunsPlayed} parties"
            : "Atteins le palier le plus haut possible. Bonne chance !";
        var recordLabel = Ui.Label(records, 16, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center);
        recordLabel.Position = new Vector2(0f, 884f);
        recordLabel.Size = new Vector2(900f, 24f);
        root.AddChild(recordLabel);

        var hint = Ui.Label("← →  choisir   ·   Entrée  jouer   ·   C  compétences   ·   Échap  quitter", 14, Pal.Alpha(Pal.TextDim, 0.6f), Fonts.Regular, HorizontalAlignment.Center);
        hint.Position = new Vector2(0f, 950f);
        hint.Size = new Vector2(900f, 22f);
        root.AddChild(hint);

        Select(_selected, silent: true);
    }

    private void Select(int index, bool silent = false)
    {
        _selected = (index + _cards.Count) % _cards.Count;
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Selected = i == _selected;
        }
        _perk.Text = Selected.Perk.Replace("\n", " ");
        if (!silent)
        {
            Sfx.Play(Sound.Click, 1.1f);
        }
    }

    private void OpenSkills()
    {
        Sfx.Play(Sound.Click);
        SaveData.SetLastCharacter(Selected.Id);
        Main.Instance.ShowSkillTree();
    }

    private void Play()
    {
        Sfx.Play(Sound.Pick);
        SaveData.SetLastCharacter(Selected.Id);
        Main.Instance.StartGame(Selected);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key)
        {
            return;
        }
        switch (key.Keycode)
        {
            case Key.Left:
            case Key.A:
            case Key.Q:
                Select(_selected - 1);
                break;
            case Key.Right:
            case Key.D:
                Select(_selected + 1);
                break;
            case Key.Enter:
            case Key.KpEnter:
            case Key.Space:
                if (!key.Echo) Play();
                break;
            case Key.C:
                if (!key.Echo) OpenSkills();
                break;
            case Key.Escape:
                GetTree().Quit();
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }
}

public partial class CharacterCard : Control
{
    public event System.Action Clicked;
    public event System.Action DoubleClicked;

    public CharacterDef Character;
    public bool Selected;

    private float _sel;
    private float _hover;
    private bool _hovered;
    private float _time;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(160f, 240f);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = Character.Perk.Replace("\n", " ");

        AddChild(Ui.Wrapped(Character.Name, 18, Pal.Text, Fonts.Bold, HorizontalAlignment.Center,
            new Vector2(4f, 176f), new Vector2(152f, 50f), 4));

        MouseEntered += () => { _hovered = true; Sfx.Play(Sound.Hover); };
        MouseExited += () => _hovered = false;
        _time = GD.Randf() * 5f;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            AcceptEvent();
            if (mb.DoubleClick)
            {
                DoubleClicked?.Invoke();
            }
            else
            {
                Clicked?.Invoke();
            }
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _sel = Mathf.MoveToward(_sel, Selected ? 1f : 0f, (float)delta * 6f);
        _hover = Mathf.MoveToward(_hover, _hovered ? 1f : 0f, (float)delta * 8f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var accent = Character.ShoeColor.Lightened(0.2f);
        var border = Pal.Gold.Lerp(accent, 0.3f);

        if (_sel > 0f)
        {
            DrawColoredPolygon(Paint.RoundedRect(rect.Grow(8f + 3f * Mathf.Sin(_time * 3f)), 22f), Pal.Alpha(Pal.Gold, 0.18f * _sel));
        }
        var body = Paint.RoundedRect(rect, 16f);
        var colors = new Color[body.Length];
        var top = new Color(0.17f, 0.08f, 0.2f).Lerp(Character.ShoeColor.Darkened(0.6f), 0.4f + 0.2f * _sel);
        var bottom = new Color(0.05f, 0.02f, 0.07f);
        for (int i = 0; i < body.Length; i++)
        {
            colors[i] = top.Lerp(bottom, body[i].Y / rect.Size.Y);
        }
        DrawPolygon(body, colors);

        var outline = Paint.Closed(body);
        var lineColor = new Color(1f, 1f, 1f, 0.15f + 0.25f * _hover).Lerp(border, _sel);
        DrawPolyline(outline, lineColor, 2f + 1.5f * _sel, true);

        // Spotlight + pedestal.
        var center = new Vector2(rect.Size.X / 2f, 128f);
        Paint.Halo(this, center + new Vector2(0f, -30f), 70f, Pal.Alpha(accent, 0.18f + 0.2f * _sel), 5);
        DrawColoredPolygon(new[] { center + new Vector2(-62f, 20f), center + new Vector2(62f, 20f), center + new Vector2(52f, 30f), center + new Vector2(-52f, 30f) },
            new Color(0f, 0f, 0f, 0.35f));

        float bob = _sel * Mathf.Sin(_time * 3f) * 3f - 4f * _sel;
        float scale = Character.Style == ShoeStyle.Boot ? 0.66f : 0.74f;
        ShoeArt.DrawShoeOnly(this, Character, new Vector2(rect.Size.X / 2f - 62f, center.Y + 20f + bob), scale, false);
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
        // Marquee frame with chase lights.
        var frame = new Rect2(120f, 70f, 660f, 300f);
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
        var title = new Vector2(450f, 196f);
        Paint.Halo(this, title, 260f, Pal.Alpha(Pal.Pink, 0.1f * glow), 6);
        Paint.TextCentered(this, Fonts.Black, title + new Vector2(0f, 6f), "PLINKO", 150, new Color(0.25f, 0.02f, 0.12f), 10, new Color(0.12f, 0.01f, 0.06f));
        Paint.TextCentered(this, Fonts.Black, title, "PLINKO", 150, Pal.Hdr(new Color(1f, 0.62f, 0.84f), 1.05f + 0.2f * glow), 5, Pal.Hdr(Pal.Pink, 0.95f * glow));

        var palace = new Vector2(450f, 312f);
        Paint.TextCentered(this, Fonts.Bold, palace, "P  A  L  A  C  E", 36, Pal.Hdr(Pal.Gold, 1.15f), 3, new Color(0.3f, 0.15f, 0.02f));
        DrawLine(palace + new Vector2(-250f, 0f), palace + new Vector2(-170f, 0f), Pal.Hdr(Pal.Gold, 1.2f), 2f);
        DrawLine(palace + new Vector2(170f, 0f), palace + new Vector2(250f, 0f), Pal.Hdr(Pal.Gold, 1.2f), 2f);

        // Tumbling decorative balls on each side.
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float t = _time * 1.3f + i * 1.7f;
            var p = new Vector2(450f + side * (385f + Mathf.Sin(t * 2f) * 12f), 90f + Mathf.PosMod(t * 90f, 300f));
            Paint.Halo(this, p, 26f, Pal.Alpha(Pal.Pink, 0.4f));
            DrawCircle(p, 9f, Pal.Hdr(new Color(1f, 0.95f, 1f), 1.2f));
        }
    }

    private void Bulb(Vector2 p, int index, int phase)
    {
        bool lit = (index + phase) % 4 < 2;
        if (lit)
        {
            DrawCircle(p, 7f, Pal.Alpha(Pal.Gold, 0.2f));
        }
        DrawCircle(p, 3f, lit ? Pal.Hdr(Pal.Gold, 2.2f) : new Color(0.35f, 0.24f, 0.12f));
    }
}
