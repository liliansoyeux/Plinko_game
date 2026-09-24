using Godot;

namespace Plinko;

// The dim casino room behind the machine: velvet wall, art-deco pinstripes, slowly
// drifting out-of-focus lights and a patterned carpet.
public partial class CasinoBackground : Node2D
{
    public Vector2 ViewportSize = new(900f, 1000f);
    public float FloorY = 900f;

    private struct Bokeh
    {
        public Vector2 Position;
        public float Radius;
        public float Speed;
        public float Phase;
        public Color Color;
    }

    private readonly Bokeh[] _bokeh = new Bokeh[26];
    private float _time;

    public override void _Ready()
    {
        ZIndex = -50;
        var rng = new RandomNumberGenerator { Seed = 42 };
        var colors = new[] { Pal.Gold, Pal.Pink, Pal.Cyan, Pal.Purple, Pal.Orange };
        for (int i = 0; i < _bokeh.Length; i++)
        {
            _bokeh[i] = new Bokeh
            {
                Position = new Vector2(rng.RandfRange(-40f, ViewportSize.X + 40f), rng.RandfRange(0f, FloorY)),
                Radius = rng.RandfRange(14f, 46f),
                Speed = rng.RandfRange(4f, 12f),
                Phase = rng.RandfRange(0f, Mathf.Tau),
                Color = colors[rng.RandiRange(0, colors.Length - 1)],
            };
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var wall = new Rect2(-200f, -200f, ViewportSize.X + 400f, FloorY + 200f);
        Paint.VerticalGradient(this, wall, new Color(0.1f, 0.025f, 0.08f), new Color(0.2f, 0.04f, 0.1f));

        // Art-deco pinstripes.
        for (float x = -200f; x < ViewportSize.X + 200f; x += 36f)
        {
            DrawLine(new Vector2(x, -200f), new Vector2(x, FloorY), new Color(1f, 0.75f, 0.4f, 0.035f), 2f);
        }

        // Wall sconces glow.
        foreach (float x in new[] { 70f, ViewportSize.X - 70f })
        {
            Paint.Halo(this, new Vector2(x, 40f), 150f, new Color(1f, 0.6f, 0.3f, 0.2f), 6);
        }

        foreach (var b in _bokeh)
        {
            float y = Mathf.PosMod(b.Position.Y - _time * b.Speed, FloorY + 100f) - 50f;
            float x = b.Position.X + Mathf.Sin(_time * 0.3f + b.Phase) * 12f;
            float twinkle = 0.5f + 0.5f * Mathf.Sin(_time * 1.3f + b.Phase);
            DrawCircle(new Vector2(x, y), b.Radius, Pal.Alpha(b.Color, 0.035f + 0.04f * twinkle));
            DrawCircle(new Vector2(x, y), b.Radius * 0.7f, Pal.Alpha(b.Color, 0.03f + 0.03f * twinkle));
        }

        // Carpet.
        var carpet = new Rect2(-200f, FloorY, ViewportSize.X + 400f, ViewportSize.Y - FloorY + 200f);
        Paint.VerticalGradient(this, carpet, new Color(0.22f, 0.03f, 0.07f), new Color(0.08f, 0.01f, 0.03f));
        for (float x = -180f; x < ViewportSize.X + 200f; x += 60f)
        {
            for (float y = FloorY + 20f; y < ViewportSize.Y + 40f; y += 40f)
            {
                float ox = ((int)((y - FloorY) / 40f) % 2) * 30f;
                var c = new Vector2(x + ox, y);
                DrawColoredPolygon(new[] { c + new Vector2(0, -8), c + new Vector2(10, 0), c + new Vector2(0, 8), c + new Vector2(-10, 0) },
                    new Color(1f, 0.7f, 0.3f, 0.06f));
            }
        }
        DrawLine(new Vector2(-200f, FloorY), new Vector2(ViewportSize.X + 200f, FloorY), new Color(1f, 0.7f, 0.35f, 0.25f), 2f);
    }
}
