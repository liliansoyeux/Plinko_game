using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// Chest reward screen: three cards, pick one (mouse or keys 1/2/3). The tree is paused
// while it's open, so this layer and everything in it processes Always.
public partial class UpgradeChoicePopup : CanvasLayer
{
    private Control _root;
    private ColorRect _dim;
    private Label _title;
    private Label _subtitle;
    private HBoxContainer _cards;
    private Action<UpgradeOption> _onChosen;
    private Func<List<UpgradeOption>> _reroll;
    private Func<int> _rerollsLeft;
    private Button _rerollButton;
    private readonly List<UpgradeCard> _cardList = new();
    private bool _choosing;

    public bool IsOpen => Visible;
    public IReadOnlyList<UpgradeCard> Cards => _cardList;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 10;
        Visible = false;

        _root = new Control();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _dim = new ColorRect { Color = new Color(0.02f, 0.0f, 0.04f, 0.78f), MouseFilter = Control.MouseFilterEnum.Stop };
        _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_dim);

        _title = Ui.Label("", 46, Pal.Gold, Fonts.Black, HorizontalAlignment.Center, 8);
        _title.Position = new Vector2(0f, 250f);
        _title.Size = new Vector2(900f, 60f);
        _root.AddChild(_title);

        _subtitle = Ui.Label("", 20, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center);
        _subtitle.Position = new Vector2(0f, 310f);
        _subtitle.Size = new Vector2(900f, 30f);
        _root.AddChild(_subtitle);

        _cards = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _cards.AddThemeConstantOverride("separation", 22);
        _cards.Position = new Vector2(0f, 360f);
        _cards.Size = new Vector2(900f, 330f);
        _root.AddChild(_cards);

        _rerollButton = new Button { CustomMinimumSize = new Vector2(240f, 44f), FocusMode = Control.FocusModeEnum.None };
        _rerollButton.Position = new Vector2(330f, 700f);
        _rerollButton.Size = new Vector2(240f, 44f);
        _rerollButton.Pressed += Reroll;
        _root.AddChild(_rerollButton);

        var hint = Ui.Label("Clique sur une carte  ·  touches 1 à 4  ·  R : relancer", 15, Pal.Alpha(Pal.TextDim, 0.7f), Fonts.Regular, HorizontalAlignment.Center);
        hint.Position = new Vector2(0f, 756f);
        hint.Size = new Vector2(900f, 24f);
        _root.AddChild(hint);
    }

    public void Open(string title, string subtitle, Color color, List<UpgradeOption> options, Action<UpgradeOption> onChosen,
        Func<List<UpgradeOption>> reroll = null, Func<int> rerollsLeft = null)
    {
        _onChosen = onChosen;
        _reroll = reroll;
        _rerollsLeft = rerollsLeft;
        _choosing = false;
        _title.Text = title;
        _title.LabelSettings.FontColor = color;
        _subtitle.Text = subtitle;

        Visible = true;
        _dim.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_dim, "modulate:a", 1f, 0.2f);

        _title.PivotOffset = _title.Size / 2f;
        Ui.PopIn(_title, 0f, 0.4f);
        ShowCards(options);
    }

    private int RerollsLeft => _reroll == null || _rerollsLeft == null ? 0 : _rerollsLeft();

    private void RefreshRerollButton()
    {
        int left = RerollsLeft;
        _rerollButton.Visible = left > 0;
        _rerollButton.Text = $"Relancer les cartes ({left})";
    }

    private void Reroll()
    {
        if (_choosing || RerollsLeft <= 0)
        {
            return;
        }
        var options = _reroll();
        if (options == null || options.Count == 0)
        {
            return;
        }
        Sfx.Play(Sound.Chest, 1.2f, -4f);
        ShowCards(options);
    }

    private void ShowCards(List<UpgradeOption> options)
    {
        foreach (var card in _cardList)
        {
            card.QueueFree();
        }
        _cardList.Clear();

        // Four cards (skill tree) don't fit at full width on a 900px screen.
        float width = options.Count >= 4 ? 196f : 236f;
        _cards.AddThemeConstantOverride("separation", options.Count >= 4 ? 14 : 22);

        var record = RunManager.Instance.Record;
        for (int i = 0; i < options.Count; i++)
        {
            var option = options[i];
            record.UpgradeCounts.TryGetValue(option.Id, out int owned);
            var card = new UpgradeCard { Option = option, Index = i, Owned = owned, Width = width };
            card.Chosen += OnCardChosen;
            _cards.AddChild(card);
            _cardList.Add(card);
        }
        RefreshRerollButton();

        // Cards need a layout pass before their pivot is meaningful.
        Callable.From(() =>
        {
            for (int i = 0; i < _cardList.Count; i++)
            {
                Ui.PopIn(_cardList[i], 0.08f + i * 0.08f, 0.4f);
            }
        }).CallDeferred();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || _choosing)
        {
            return;
        }
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.R)
            {
                GetViewport().SetInputAsHandled();
                Reroll();
                return;
            }
            int index = key.Keycode switch
            {
                Key.Key1 or Key.Kp1 => 0,
                Key.Key2 or Key.Kp2 => 1,
                Key.Key3 or Key.Kp3 => 2,
                Key.Key4 or Key.Kp4 => 3,
                _ => -1
            };
            if (index >= 0 && index < _cardList.Count)
            {
                GetViewport().SetInputAsHandled();
                OnCardChosen(_cardList[index]);
            }
        }
    }

    public void ChooseIndex(int index)
    {
        if (Visible && !_choosing && index >= 0 && index < _cardList.Count)
        {
            OnCardChosen(_cardList[index]);
        }
    }

    private void OnCardChosen(UpgradeCard chosen)
    {
        if (_choosing)
        {
            return;
        }
        _choosing = true;
        Sfx.Play(chosen.Option.Rarity == Rarity.Cursed ? Sound.Malus : Sound.Pick);

        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.SetParallel(true);
        foreach (var card in _cardList)
        {
            if (card == chosen)
            {
                card.PivotOffset = card.Size / 2f;
                tween.TweenProperty(card, "scale", new Vector2(1.12f, 1.12f), 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            }
            else
            {
                tween.TweenProperty(card, "modulate:a", 0.15f, 0.18f);
            }
        }
        tween.Chain().TweenInterval(0.22f);
        tween.Chain().TweenProperty(_root, "modulate:a", 0f, 0.15f);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            Visible = false;
            _root.Modulate = Colors.White;
            _onChosen?.Invoke(chosen.Option);
        }));
    }
}

public partial class UpgradeCard : Control
{
    public event Action<UpgradeCard> Chosen;

    public UpgradeOption Option;
    public int Index;
    public int Owned;
    public float Width = 236f;

    private float _hover;
    private bool _hovered;
    private float _time;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Width, 320f);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;

        var color = Pal.ForRarity(Option.Rarity);

        bool narrow = Width < 220f;
        AddChild(Ui.Wrapped(Option.Label, narrow ? 20 : 23, Colors.White, Fonts.Bold, HorizontalAlignment.Center,
            new Vector2(10f, 174f), new Vector2(Width - 20f, 56f), 4));
        AddChild(Ui.Wrapped(Option.Description, narrow ? 14 : 16, Pal.TextDim, Fonts.Regular, HorizontalAlignment.Center,
            new Vector2(12f, 232f), new Vector2(Width - 24f, 80f)));

        if (Owned > 0)
        {
            // Top-right corner, clear of the name/description text.
            AddChild(Ui.Wrapped($"x{Owned}", 15, Pal.Alpha(color, 0.95f), Fonts.Bold, HorizontalAlignment.Right,
                new Vector2(Width - 58f, 16f), new Vector2(44f, 22f)));
        }

        MouseEntered += () => { _hovered = true; Sfx.Play(Sound.Hover); };
        MouseExited += () => _hovered = false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            AcceptEvent();
            Chosen?.Invoke(this);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _hover = Mathf.MoveToward(_hover, _hovered ? 1f : 0f, (float)delta * 7f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var color = Option.Rarity == Rarity.Legendary ? Pal.Prismatic(_time) : Pal.ForRarity(Option.Rarity);
        var rect = new Rect2(Vector2.Zero, Size);
        // Shadow + glow.
        DrawColoredPolygon(Paint.RoundedRect(rect.Grow(10f + 6f * _hover), 26f), Pal.Alpha(color, 0.08f + 0.18f * _hover));
        DrawColoredPolygon(Paint.RoundedRect(new Rect2(rect.Position + new Vector2(0f, 10f), rect.Size), 18f), new Color(0, 0, 0, 0.45f));

        var body = Paint.RoundedRect(rect, 18f);
        var colors = new Color[body.Length];
        var top = new Color(0.16f, 0.08f, 0.22f).Lerp(color.Darkened(0.6f), 0.35f);
        var bottom = new Color(0.06f, 0.03f, 0.09f);
        for (int i = 0; i < body.Length; i++)
        {
            colors[i] = top.Lerp(bottom, body[i].Y / rect.Size.Y);
        }
        DrawPolygon(body, colors);
        var outline = Paint.Closed(body);
        DrawPolyline(outline, color.Lerp(Colors.White, 0.3f * _hover), 2.5f + _hover, true);

        // Rarity ribbon.
        var ribbon = new Rect2(rect.Size.X / 2f - 58f, 14f, 116f, 26f);
        DrawColoredPolygon(Paint.RoundedRect(ribbon, 13f), Pal.Alpha(color, 0.2f));
        DrawPolyline(Paint.Closed(Paint.RoundedRect(ribbon, 13f)), Pal.Alpha(color, 0.7f), 1.5f, true);
        Paint.TextCentered(this, Fonts.Bold, ribbon.GetCenter(), Pal.RarityLabel(Option.Rarity), 13, color.Lightened(0.2f));

        // Icon medallion.
        var iconCenter = new Vector2(rect.Size.X / 2f, 106f);
        float spin = _time * 0.6f;
        for (int i = 0; i < 12; i++)
        {
            float a = spin + i * Mathf.Tau / 12f;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(iconCenter + d * 50f, iconCenter + d * (58f + 4f * _hover), Pal.Alpha(color, 0.25f + 0.25f * _hover), 2f);
        }
        DrawCircle(iconCenter, 46f, color.Darkened(0.7f));
        DrawArc(iconCenter, 46f, 0f, Mathf.Tau, 48, color, 2f, true);
        UpgradeIcons.Draw(this, iconCenter, 42f, Option.Icon, color);

        // Key hint.
        Paint.TextCentered(this, Fonts.Bold, new Vector2(24f, 26f), $"{Index + 1}", 16, Pal.Alpha(Pal.TextDim, 0.7f));
    }
}
