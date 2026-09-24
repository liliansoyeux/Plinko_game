using Godot;
using System.Collections.Generic;

namespace Plinko;

// Big centered announcements ("PALIER 3", "BOSS", "PALIER RÉUSSI"). Queued so two
// announcements fired back to back play one after the other instead of overlapping.
public partial class Banner : CanvasLayer
{
    private readonly Queue<(string title, string subtitle, Color color, float hold)> _queue = new();
    private Control _band;
    private Label _title;
    private Label _subtitle;
    private bool _playing;

    public override void _Ready()
    {
        Layer = 8;
        ProcessMode = ProcessModeEnum.Always;

        _band = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _band.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_band);

        var stripe = new BannerStripe { MouseFilter = Control.MouseFilterEnum.Ignore };
        stripe.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _band.AddChild(stripe);

        _title = Ui.Label("", 64, Pal.Text, Fonts.Black, HorizontalAlignment.Center, 10);
        _title.Position = new Vector2(0f, 400f);
        _title.Size = new Vector2(900f, 90f);
        _band.AddChild(_title);

        _subtitle = Ui.Wrapped("", 22, Pal.Text, Fonts.Bold, HorizontalAlignment.Center,
            new Vector2(40f, 486f), new Vector2(820f, 60f), 6);
        _band.AddChild(_subtitle);
    }

    public void Show(string title, string subtitle, Color color, float hold = 1.2f)
    {
        _queue.Enqueue((title, subtitle, color, hold));
        if (!_playing)
        {
            PlayNext();
        }
    }

    private void PlayNext()
    {
        if (_queue.Count == 0)
        {
            _playing = false;
            _band.Visible = false;
            return;
        }

        _playing = true;
        var (title, subtitle, color, hold) = _queue.Dequeue();
        _title.Text = title;
        _title.LabelSettings.FontColor = color.Lightened(0.2f);
        _title.LabelSettings.OutlineColor = color.Darkened(0.75f);
        _subtitle.Text = subtitle;
        ((BannerStripe)_band.GetChild(0)).Color = color;

        _band.Visible = true;
        _band.Modulate = new Color(1, 1, 1, 0);
        _title.PivotOffset = _title.Size / 2f;
        _title.Scale = new Vector2(1.6f, 1.6f);

        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.SetParallel(true);
        tween.TweenProperty(_band, "modulate:a", 1f, 0.18f);
        tween.TweenProperty(_title, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenInterval(hold);
        tween.Chain().TweenProperty(_band, "modulate:a", 0f, 0.3f);
        tween.Chain().TweenCallback(Callable.From(PlayNext));
    }
}

public partial class BannerStripe : Control
{
    public Color Color = Pal.Pink;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var band = new Rect2(-10f, 392f, 920f, 158f);
        var clear = Pal.Alpha(Pal.Night, 0f);
        var dark = Pal.Alpha(new Color(0.03f, 0.01f, 0.05f), 0.88f);
        DrawPolygon(new[] { band.Position, new Vector2(band.End.X, band.Position.Y), new Vector2(band.End.X, band.Position.Y + 30f), new Vector2(band.Position.X, band.Position.Y + 30f) },
            new[] { clear, clear, dark, dark });
        DrawRect(new Rect2(band.Position.X, band.Position.Y + 30f, band.Size.X, band.Size.Y - 60f), dark);
        DrawPolygon(new[] { new Vector2(band.Position.X, band.End.Y - 30f), new Vector2(band.End.X, band.End.Y - 30f), band.End, new Vector2(band.Position.X, band.End.Y) },
            new[] { dark, dark, clear, clear });
        DrawLine(new Vector2(0f, band.Position.Y + 30f), new Vector2(900f, band.Position.Y + 30f), Color, 2f);
        DrawLine(new Vector2(0f, band.End.Y - 30f), new Vector2(900f, band.End.Y - 30f), Color, 2f);
    }
}
