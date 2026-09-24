using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// Full-screen skill tree over the game (which is paused meanwhile). Three branches of four
// nodes bought with jetons; each node needs at least one level in the one above it.
public partial class SkillTreeOverlay : CanvasLayer
{
    public event Action Closed;

    private const float ColumnWidth = 300f;
    private const float ColumnGap = 40f;
    private const float FirstRowY = 214f;
    private const float RowStep = 172f;
    private const float CardHeight = 136f;

    private readonly List<SkillNodeCard> _cards = new();
    private Label _jetons;
    private Button _reset;
    private bool _resetArmed;
    private double _resetTimer;

    public IReadOnlyList<SkillNodeCard> Cards => _cards;

    public static Color BranchColor(SkillBranch branch) => branch switch
    {
        SkillBranch.Fortune => Pal.Gold,
        SkillBranch.Automation => Pal.Cyan,
        _ => Pal.Green,
    };

    private static string BranchName(SkillBranch branch) => branch switch
    {
        SkillBranch.Fortune => "FORTUNE",
        SkillBranch.Automation => "AUTOMATISATION",
        _ => "ÉCONOMIE",
    };

    public static Vector2 CardPosition(SkillNode node)
    {
        float left = (1500f - 3f * ColumnWidth - 2f * ColumnGap) / 2f;
        return new Vector2(left + (int)node.Branch * (ColumnWidth + ColumnGap), FirstRowY + node.Tier * RowStep);
    }

    public override void _Ready()
    {
        Layer = 18;
        ProcessMode = ProcessModeEnum.Always;

        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var dim = new ColorRect { Color = new Color(0.03f, 0.01f, 0.05f, 0.95f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var title = Ui.Label("ARBRE DE COMPÉTENCES", 44, Pal.Gold, Fonts.Black, HorizontalAlignment.Center, 8);
        title.Position = new Vector2(0f, 26f);
        title.Size = new Vector2(1500f, 60f);
        root.AddChild(title);

        var subtitle = Ui.Label("Payé en jetons (gagnés en changeant de chaussures). Les bonus sont permanents.", 16, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center);
        subtitle.Position = new Vector2(0f, 84f);
        subtitle.Size = new Vector2(1500f, 24f);
        root.AddChild(subtitle);

        _jetons = Ui.Label("", 28, Pal.Purple.Lightened(0.3f), Fonts.Black, HorizontalAlignment.Center, 4);
        _jetons.Position = new Vector2(0f, 116f);
        _jetons.Size = new Vector2(1500f, 44f);
        root.AddChild(_jetons);

        var lines = new SkillTreeLines { MouseFilter = Control.MouseFilterEnum.Ignore };
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
            card.BuyRequested += Buy;
            root.AddChild(card);
            _cards.Add(card);
        }

        var back = new Button { Text = "Retour au jeu", Position = new Vector2(510f, 918f), Size = new Vector2(220f, 52f) };
        back.Pressed += Close;
        root.AddChild(back);

        _reset = new Button { Text = "Réinitialiser", Position = new Vector2(770f, 918f), Size = new Vector2(220f, 52f) };
        _reset.Pressed += OnReset;
        root.AddChild(_reset);

        Refresh();
    }

    public void Buy(SkillNodeCard card)
    {
        if (IdleManager.Instance.BuySkill(card.Node))
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

    public void Close()
    {
        Sfx.Play(Sound.Click);
        Closed?.Invoke();
        QueueFree();
    }

    private void OnReset()
    {
        if (!_resetArmed)
        {
            _resetArmed = true;
            _resetTimer = 3.0;
            _reset.Text = "Confirmer ?";
            return;
        }
        _resetArmed = false;
        _reset.Text = "Réinitialiser";
        IdleManager.Instance.ResetSkills();
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
            Close();
        }
    }

    private void Refresh()
    {
        _jetons.Text = $"{IdleManager.Instance.Jetons} jetons";
        _reset.Disabled = IdleManager.Instance.SpentJetons() == 0;
    }
}

public partial class SkillTreeLines : Control
{
    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var idle = IdleManager.Instance;
        foreach (var node in SkillTree.Nodes)
        {
            if (node.RequiresId == null) continue;
            var parent = SkillTree.Find(node.RequiresId);
            var from = SkillTreeOverlay.CardPosition(parent) + new Vector2(150f, 136f);
            var to = SkillTreeOverlay.CardPosition(node) + new Vector2(150f, 0f);
            bool lit = idle.SkillLevel(parent.Id) > 0;
            var color = SkillTreeOverlay.BranchColor(node.Branch);
            DrawLine(from, to, lit ? Pal.Hdr(color, 1.2f) : new Color(1f, 1f, 1f, 0.12f), lit ? 4f : 2f, true);
        }
    }
}

public partial class SkillNodeCard : Control
{
    public event Action<SkillNodeCard> BuyRequested;
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
        AddChild(Ui.Wrapped(Node.Name, 18, Pal.Text, Fonts.Bold, HorizontalAlignment.Left, new Vector2(72f, 10f), new Vector2(Size.X - 80f, 26f), 3));
        AddChild(Ui.Wrapped(Node.Description, 13, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Left, new Vector2(72f, 36f), new Vector2(Size.X - 82f, 66f)));
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
        var idle = IdleManager.Instance;
        int level = idle.SkillLevel(Node.Id);
        bool maxed = level >= Node.MaxLevel;
        bool unlocked = idle.IsSkillUnlocked(Node);
        bool affordable = idle.CanBuySkill(Node);
        var branch = SkillTreeOverlay.BranchColor(Node.Branch);
        var accent = maxed ? Pal.Gold : unlocked ? branch : new Color(0.4f, 0.37f, 0.45f);

        DrawSetTransform(new Vector2(Mathf.Sin(_time * 60f) * 5f * _shake, 0f), 0f, Vector2.One);
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

        for (int i = 0; i < Node.MaxLevel; i++)
        {
            var p = new Vector2(80f + i * 18f, Size.Y - 20f);
            DrawCircle(p, 6f, new Color(0f, 0f, 0f, 0.5f));
            if (i < level) DrawCircle(p, 5f, Pal.Hdr(accent, 1.2f));
            else DrawArc(p, 5f, 0f, Mathf.Tau, 16, Pal.Alpha(accent, 0.6f), 1.5f, true);
        }

        string status;
        Color statusColor;
        if (maxed) { status = "MAX"; statusColor = Pal.Gold; }
        else if (!unlocked) { status = "Verrouillé"; statusColor = new Color(0.55f, 0.5f, 0.6f); }
        else { status = $"{idle.SkillCost(Node)} jetons"; statusColor = affordable ? Pal.Green : Pal.Red; }
        var size = Fonts.Bold.GetStringSize(status, HorizontalAlignment.Left, -1, 16);
        DrawString(Fonts.Bold, new Vector2(Size.X - 14f - size.X, Size.Y - 14f), status, HorizontalAlignment.Left, -1, 16, statusColor);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
