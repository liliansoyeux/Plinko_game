using Godot;
using System.Collections.Generic;

namespace Plinko;

// Meta-progression screen: three branches of four nodes, bought with chips earned at the end
// of each run. A node needs at least one level in the node above it.
public partial class SkillTreeScreen : Node2D
{
    private const float ColumnWidth = 262f;
    private const float ColumnGap = 26f;
    private const float FirstRowY = 214f;
    private const float RowStep = 172f;
    private const float CardHeight = 136f;

    private readonly List<SkillNodeCard> _cards = new();
    private Label _chips;
    private Button _reset;
    private bool _resetArmed;
    private double _resetTimer;

    public IReadOnlyList<SkillNodeCard> Cards => _cards;

    public static Color BranchColor(SkillBranch branch) => branch switch
    {
        SkillBranch.Fortune => Pal.Gold,
        SkillBranch.Chance => Pal.Cyan,
        _ => Pal.Green,
    };

    private static string BranchName(SkillBranch branch) => branch switch
    {
        SkillBranch.Fortune => "FORTUNE",
        SkillBranch.Chance => "CHANCE",
        _ => "SÉCURITÉ",
    };

    public static Vector2 CardPosition(SkillNode node)
    {
        float left = (900f - 3f * ColumnWidth - 2f * ColumnGap) / 2f;
        return new Vector2(left + (int)node.Branch * (ColumnWidth + ColumnGap), FirstRowY + node.Tier * RowStep);
    }

    public override void _Ready()
    {
        AddChild(new CasinoBackground());

        var layer = new CanvasLayer { Layer = 2 };
        AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var title = Ui.Label("ARBRE DE COMPÉTENCES", 44, Pal.Gold, Fonts.Black, HorizontalAlignment.Center, 8);
        title.Position = new Vector2(0f, 26f);
        title.Size = new Vector2(900f, 60f);
        root.AddChild(title);

        var subtitle = Ui.Label("Gagne des jetons à chaque fin de partie. Les bonus s'appliquent à toutes tes parties.", 16, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center);
        subtitle.Position = new Vector2(0f, 84f);
        subtitle.Size = new Vector2(900f, 24f);
        root.AddChild(subtitle);

        root.AddChild(new ChipBadge { Position = new Vector2(360f, 116f), Size = new Vector2(180f, 44f) });
        _chips = Ui.Label("", 26, Pal.Gold, Fonts.Black, HorizontalAlignment.Left, 4);
        _chips.Position = new Vector2(410f, 116f);
        _chips.Size = new Vector2(160f, 44f);
        root.AddChild(_chips);

        var lines = new TreeLines { MouseFilter = Control.MouseFilterEnum.Ignore };
        lines.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(lines);

        for (int b = 0; b < 3; b++)
        {
            var branch = (SkillBranch)b;
            var header = Ui.Label(BranchName(branch), 20, BranchColor(branch), Fonts.Bold, HorizontalAlignment.Center, 4);
            var pos = CardPosition(new SkillNode { Branch = branch, Tier = 0 });
            header.Position = new Vector2(pos.X, 176f);
            header.Size = new Vector2(ColumnWidth, 28f);
            root.AddChild(header);
        }

        foreach (var node in SkillTree.Nodes)
        {
            var card = new SkillNodeCard { Node = node, Position = CardPosition(node), Size = new Vector2(ColumnWidth, CardHeight) };
            card.BuyRequested += OnBuy;
            root.AddChild(card);
            _cards.Add(card);
        }

        var back = new Button { Text = "Retour", Position = new Vector2(230f, 918f), Size = new Vector2(200f, 52f) };
        back.MouseEntered += () => Sfx.Play(Sound.Hover);
        back.Pressed += () => { Sfx.Play(Sound.Click); Main.Instance.ShowTitle(); };
        root.AddChild(back);

        _reset = new Button { Text = "Réinitialiser", Position = new Vector2(470f, 918f), Size = new Vector2(200f, 52f) };
        _reset.MouseEntered += () => Sfx.Play(Sound.Hover);
        _reset.Pressed += OnReset;
        root.AddChild(_reset);

        Refresh();
    }

    public void OnBuy(SkillNodeCard card)
    {
        if (SkillTree.Buy(card.Node))
        {
            Sfx.Play(Sound.Pick);
            card.Celebrate();
        }
        else
        {
            Sfx.Play(Sound.Click, 0.6f);
            card.Deny();
        }
        Refresh();
    }

    // Two-step confirm: the first press arms the button for a few seconds.
    private void OnReset()
    {
        if (!_resetArmed)
        {
            _resetArmed = true;
            _resetTimer = 3.0;
            _reset.Text = "Confirmer ?";
            Sfx.Play(Sound.Click);
            return;
        }
        _resetArmed = false;
        _reset.Text = "Réinitialiser";
        SkillTree.ResetAll();
        Sfx.Play(Sound.Malus);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_resetArmed)
        {
            _resetTimer -= delta;
            if (_resetTimer <= 0)
            {
                _resetArmed = false;
                _reset.Text = "Réinitialiser";
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Main.Instance.ShowTitle();
        }
    }

    private void Refresh()
    {
        _chips.Text = $"{SaveData.Chips} jetons";
        _reset.Disabled = SkillTree.SpentChips() == 0;
        foreach (var card in _cards)
        {
            card.QueueRedraw();
        }
    }
}

public partial class ChipBadge : Control
{
    public override void _Draw()
    {
        var c = new Vector2(24f, Size.Y / 2f);
        DrawCircle(c, 17f, Pal.Gold.Darkened(0.35f));
        DrawCircle(c, 14f, Pal.Gold);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8f;
            DrawLine(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 10f, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f, Colors.White, 2.5f);
        }
        DrawCircle(c, 8f, Pal.Gold.Darkened(0.2f));
    }
}

public partial class TreeLines : Control
{
    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        foreach (var node in SkillTree.Nodes)
        {
            if (node.RequiresId == null)
            {
                continue;
            }
            var parent = SkillTree.Find(node.RequiresId);
            var from = SkillTreeScreen.CardPosition(parent) + new Vector2(131f, 136f);
            var to = SkillTreeScreen.CardPosition(node) + new Vector2(131f, 0f);
            bool lit = SkillTree.Has(parent.Id);
            var color = SkillTreeScreen.BranchColor(node.Branch);
            DrawLine(from, to, lit ? Pal.Hdr(color, 1.2f) : new Color(1f, 1f, 1f, 0.12f), lit ? 4f : 2f, true);
        }
    }
}

public partial class SkillNodeCard : Control
{
    public event System.Action<SkillNodeCard> BuyRequested;

    public SkillNode Node;

    private bool _hovered;
    private float _hover;
    private float _flash;
    private float _shake;
    private float _time;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;

        AddChild(Ui.Wrapped(Node.Name, 18, Pal.Text, Fonts.Bold, HorizontalAlignment.Left,
            new Vector2(72f, 10f), new Vector2(Size.X - 80f, 26f), 3));
        AddChild(Ui.Wrapped(Node.Description, 13, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Left,
            new Vector2(72f, 36f), new Vector2(Size.X - 82f, 66f)));

        MouseEntered += () => { _hovered = true; Sfx.Play(Sound.Hover); };
        MouseExited += () => _hovered = false;
    }

    public void Celebrate() => _flash = 1f;
    public void Deny() => _shake = 1f;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            AcceptEvent();
            BuyRequested?.Invoke(this);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _hover = Mathf.MoveToward(_hover, _hovered ? 1f : 0f, (float)delta * 8f);
        _flash = Mathf.Max(0f, _flash - (float)delta * 2f);
        _shake = Mathf.Max(0f, _shake - (float)delta * 4f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        int level = SkillTree.Level(Node.Id);
        bool maxed = level >= Node.MaxLevel;
        bool unlocked = SkillTree.IsUnlocked(Node);
        bool affordable = SkillTree.CanBuy(Node);
        var branch = SkillTreeScreen.BranchColor(Node.Branch);
        var accent = maxed ? Pal.Gold : unlocked ? branch : new Color(0.4f, 0.37f, 0.45f);

        float dx = Mathf.Sin(_time * 60f) * 5f * _shake;
        DrawSetTransform(new Vector2(dx, 0f), 0f, Vector2.One);

        var rect = new Rect2(Vector2.Zero, Size);
        if (affordable || _flash > 0f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f);
            DrawColoredPolygon(Paint.RoundedRect(rect.Grow(6f + 4f * _flash), 18f), Pal.Alpha(accent, 0.12f + 0.12f * pulse + 0.4f * _flash));
        }

        var body = Paint.RoundedRect(rect, 14f);
        var top = unlocked ? new Color(0.16f, 0.08f, 0.22f).Lerp(accent.Darkened(0.7f), 0.35f) : new Color(0.09f, 0.07f, 0.11f);
        var colors = new Color[body.Length];
        for (int i = 0; i < body.Length; i++)
        {
            colors[i] = top.Lerp(new Color(0.05f, 0.03f, 0.08f), body[i].Y / rect.Size.Y);
        }
        DrawPolygon(body, colors);
        DrawPolyline(Paint.Closed(body), accent.Lerp(Colors.White, 0.25f * _hover), 2f + (maxed ? 1f : 0f) + _hover, true);

        var iconCenter = new Vector2(38f, 56f);
        DrawCircle(iconCenter, 27f, accent.Darkened(0.7f));
        DrawArc(iconCenter, 27f, 0f, Mathf.Tau, 40, accent, 2f, true);
        UpgradeIcons.Draw(this, iconCenter, 25f, Node.Icon, unlocked ? accent : new Color(0.5f, 0.47f, 0.55f));

        // Level pips.
        for (int i = 0; i < Node.MaxLevel; i++)
        {
            var p = new Vector2(80f + i * 18f, Size.Y - 20f);
            DrawCircle(p, 6f, new Color(0f, 0f, 0f, 0.5f));
            if (i < level)
            {
                DrawCircle(p, 5f, Pal.Hdr(accent, 1.2f));
            }
            else
            {
                DrawArc(p, 5f, 0f, Mathf.Tau, 16, Pal.Alpha(accent, 0.6f), 1.5f, true);
            }
        }

        string status;
        Color statusColor;
        if (maxed)
        {
            status = "MAX";
            statusColor = Pal.Gold;
        }
        else if (!unlocked)
        {
            status = "Verrouillé";
            statusColor = new Color(0.55f, 0.5f, 0.6f);
        }
        else
        {
            status = $"{SkillTree.NextCost(Node)} jetons";
            statusColor = affordable ? Pal.Green : Pal.Red;
        }
        var font = Fonts.Bold;
        var size = font.GetStringSize(status, HorizontalAlignment.Left, -1, 16);
        DrawString(font, new Vector2(Size.X - 14f - size.X, Size.Y - 14f), status, HorizontalAlignment.Left, -1, 16, statusColor);

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
