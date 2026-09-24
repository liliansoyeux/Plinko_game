using Godot;
using System;

namespace Plinko;

// A chest floating in a gap between pegs. Touching it with a ball opens it: regular chests
// come from level-ups (upgrade choice), cursed ones from the malus gauge (malus choice).
public partial class Chest : Area2D
{
    public event Action<Chest> Opened;

    public Rarity Rarity = Rarity.Common;
    public bool IsCursed => Rarity == Rarity.Cursed;
    public Vector2I Cell;
    public float Unit = 1f;

    private bool _opened;
    private float _time;
    private float _appear;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = PhysicsLayers.Ball;
        Monitorable = false;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(30f, 26f) * Unit } });
        BodyEntered += OnBodyEntered;
        _time = GD.Randf() * 10f;
        ZIndex = 3;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_opened || body is not Ball)
        {
            return;
        }
        _opened = true;
        Opened?.Invoke(this);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _appear = Mathf.Min(1f, _appear + (float)delta * 3f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float appear = 1f - Mathf.Pow(1f - _appear, 3f);
        float bob = Mathf.Sin(_time * 2.4f) * 2.5f * Unit;
        float s = Unit * (0.3f + 0.7f * appear) * (1f + 0.25f * Mathf.Max(0f, 1f - _appear * 2f));
        DrawSetTransform(new Vector2(0f, bob), Mathf.Sin(_time * 1.7f) * 0.06f, new Vector2(s, s));

        var aura = IsCursed ? Pal.Purple : Rarity == Rarity.Legendary ? Pal.Prismatic(_time) : Pal.ForRarity(Rarity);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f);
        Paint.Halo(this, Vector2.Zero, 30f + 6f * pulse, Pal.Alpha(aura, 0.45f + 0.25f * pulse), 5);

        // Rotating rays behind rare/epic chests.
        if (Rarity is Rarity.Rare or Rarity.Epic or Rarity.Legendary)
        {
            int rays = Rarity switch { Rarity.Legendary => 12, Rarity.Epic => 8, _ => 5 };
            for (int i = 0; i < rays; i++)
            {
                float a = _time * 0.8f + i * Mathf.Tau / rays;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var side = new Vector2(-dir.Y, dir.X) * 3.5f;
                DrawColoredPolygon(new[] { dir * 10f + side, dir * 10f - side, dir * 34f },
                    Pal.Alpha(Pal.Hdr(aura, 1.5f), 0.35f));
            }
        }

        var wood = IsCursed ? new Color(0.3f, 0.1f, 0.38f) : new Color(0.72f, 0.4f, 0.16f);
        var woodDark = wood.Darkened(0.45f);
        var trim = IsCursed ? Pal.Purple : (Rarity == Rarity.Common ? Pal.Gold : aura);
        var trimLit = Pal.Hdr(trim, 1.1f + 0.5f * pulse);

        // Body with plank lines.
        var body = new Rect2(-15f, -3f, 30f, 16f);
        DrawRect(body, wood);
        DrawLine(new Vector2(-15f, 3f), new Vector2(15f, 3f), woodDark, 1f);
        DrawLine(new Vector2(-15f, 8f), new Vector2(15f, 8f), woodDark, 1f);
        DrawRect(new Rect2(-15f, 10f, 30f, 3f), woodDark);

        // Domed lid.
        var lid = new Vector2[14];
        for (int i = 0; i <= 12; i++)
        {
            float a = Mathf.Pi + i * Mathf.Pi / 12f;
            lid[i] = new Vector2(Mathf.Cos(a) * 15f, -3f + Mathf.Sin(a) * 10f);
        }
        lid[13] = new Vector2(-15f, -3f);
        DrawColoredPolygon(lid, wood.Lightened(0.12f));
        DrawPolyline(lid, trimLit, 1.6f, true);

        // Metal band, corner caps and lock plate.
        DrawRect(new Rect2(-15.5f, -4.5f, 31f, 3f), trimLit);
        DrawRect(new Rect2(-15.5f, 9f, 5f, 4.5f), trim);
        DrawRect(new Rect2(10.5f, 9f, 5f, 4.5f), trim);
        DrawRect(new Rect2(-4f, -6f, 8f, 9f), trimLit);
        DrawCircle(new Vector2(0f, -2.5f), 1.4f, woodDark);
        DrawRect(new Rect2(-0.6f, -2f, 1.2f, 3f), woodDark);
        // Sparkle glint.
        float glint = Mathf.Max(0f, Mathf.Sin(_time * 3f));
        DrawCircle(new Vector2(-8f, -9f), 1.2f + glint, Pal.Hdr(Colors.White, 1.2f * glint));

        if (IsCursed)
        {
            DrawCircle(new Vector2(0f, 7f), 3.2f, Pal.Hdr(new Color(0.9f, 0.85f, 1f), 1.1f));
            DrawCircle(new Vector2(-1.2f, 6.6f), 0.9f, woodDark);
            DrawCircle(new Vector2(1.2f, 6.6f), 0.9f, woodDark);
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
