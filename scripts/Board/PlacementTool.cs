using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

public enum PlaceableKind
{
    Blocker,
    Portal,
}

// What a board must expose for interactive placement.
public interface IPlacementBoard
{
    float Spacing { get; }
    float Unit { get; }
    Vector2 InstructionAnchor { get; }
    Vector2 CellCenter(Vector2I cell);
    List<Vector2I> FreeCellsFor(PlaceableKind kind);
    void CommitPlacement(PlaceableKind kind, Vector2I cell, float rotation);
}

// Interactive placement of a bonus blocker or portal. The game is paused meanwhile, so this
// node processes Always: a ghost follows the mouse, snapped to free gaps between pegs.
// Wheel / right-click / R rotate a blocker, left-click / Enter confirm.
public partial class PlacementTool : Node2D
{
    public IPlacementBoard Board;
    public PlaceableKind Kind;
    public event Action Placed;

    private const float RotationStep = 15f;
    private const float MaxRotation = 75f;

    private float _rotationDegrees = -20f;
    private Vector2I? _cell;
    private float _time;
    private List<Vector2I> _freeCells = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        ZIndex = 60;
        _freeCells = Board.FreeCellsFor(Kind);
        SnapTo(GetLocalMousePosition());
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion:
                SnapTo(GetLocalMousePosition());
                break;
            case InputEventMouseButton { Pressed: true } mb:
                SnapTo(GetLocalMousePosition());
                if (mb.ButtonIndex == MouseButton.Left) Confirm();
                else if (mb.ButtonIndex is MouseButton.Right or MouseButton.WheelDown) Rotate(1);
                else if (mb.ButtonIndex == MouseButton.WheelUp) Rotate(-1);
                break;
            case InputEventKey { Pressed: true } key:
                if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space) Confirm();
                else if (key.Keycode == Key.R) Rotate(1);
                else return;
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Rotate(int direction)
    {
        if (Kind != PlaceableKind.Blocker)
        {
            return;
        }
        _rotationDegrees += direction * RotationStep;
        if (_rotationDegrees > MaxRotation) _rotationDegrees = -MaxRotation;
        if (_rotationDegrees < -MaxRotation) _rotationDegrees = MaxRotation;
        Sfx.Play(Sound.Hover, 0.8f);
    }

    private void SnapTo(Vector2 local)
    {
        Vector2I? best = null;
        float bestDistance = float.MaxValue;
        foreach (var cell in _freeCells)
        {
            float d = Board.CellCenter(cell).DistanceSquaredTo(local);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = cell;
            }
        }
        if (best != _cell && best != null && _cell != null)
        {
            Sfx.Play(Sound.Hover, 1.3f, -8f);
        }
        _cell = best;
    }

    public void Confirm()
    {
        if (_cell == null || !IsInsideTree())
        {
            return;
        }
        Board.CommitPlacement(Kind, _cell.Value, Mathf.DegToRad(_rotationDegrees));
        Sfx.Play(Kind == PlaceableKind.Portal ? Sound.Portal : Sound.Place);
        QueueFree();
        Placed?.Invoke();
    }

    // Autopilot helper.
    public void ConfirmAt(Vector2 local)
    {
        SnapTo(local);
        Confirm();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float s = Board.Spacing;
        var accent = Kind == PlaceableKind.Portal ? Pal.Prismatic(_time) : Pal.Cyan;

        foreach (var cell in _freeCells)
        {
            DrawCircle(Board.CellCenter(cell), 2.5f, Pal.Alpha(accent, 0.35f));
        }

        if (_cell != null)
        {
            var p = Board.CellCenter(_cell.Value);
            float bob = 0.85f + 0.15f * Mathf.Sin(_time * 6f);
            if (Kind == PlaceableKind.Portal)
            {
                Portal.DrawPortal(this, p, s * 0.42f, _time, 0f, 0.55f + 0.35f * bob);
            }
            else
            {
                var size = new Vector2(s * 0.85f, 8f * Board.Unit);
                DrawSetTransform(p, Mathf.DegToRad(_rotationDegrees), Vector2.One);
                var rect = new Rect2(-size / 2f, size);
                DrawColoredPolygon(Paint.RoundedRect(rect.Grow(5f), size.Y / 2f + 5f), Pal.Alpha(Pal.Cyan, 0.25f * bob));
                DrawColoredPolygon(Paint.RoundedRect(rect, size.Y / 2f), Pal.Alpha(Pal.Hdr(Pal.Cyan, 1.5f), 0.8f * bob));
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
        }

        string title = Kind == PlaceableKind.Portal ? "PLACE TON PORTAIL" : "PLACE TON BÂTON";
        string help = Kind == PlaceableKind.Portal
            ? "Clic : poser"
            : "Clic : poser   ·   Molette / clic droit : tourner";
        var top = Board.InstructionAnchor;
        Paint.TextCentered(this, Fonts.Black, top, title, 30, Pal.Hdr(accent, 1.2f), 6);
        Paint.TextCentered(this, Fonts.Bold, top + new Vector2(0f, 30f), help, 16, Pal.Text, 4);
    }
}
