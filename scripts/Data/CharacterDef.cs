using Godot;
using System.Collections.Generic;

namespace Plinko;

public enum ShoeStyle
{
    Derby,
    Sneaker,
    Heel,
    Boot,
    FlipFlop,
}

// A pair of shoes = a difficulty level. Harder pairs are unlocked one after the other by
// earning enough in a single run, and pay out far more jetons when you prestige with them.
// They're also what you see resting on the machine.
public class CharacterDef
{
    public string Id;
    public string Name;
    public ShoeStyle Style;
    public Color ShoeColor;
    public Color AccentColor;
    public Color PantsColor;

    // Difficulty
    public double CostMultiplier = 1.0;
    public double SlotMultiplier = 1.0;
    public double CadenceMultiplier = 1.0;
    public double JetonMultiplier = 1.0;
    public double UnlockRunEarnings;   // earned in a single run with the previous pair

    public string DifficultyText
    {
        get
        {
            var parts = new List<string>();
            if (CostMultiplier > 1.0) parts.Add($"Prix x{CostMultiplier:0.#}");
            if (SlotMultiplier < 1.0) parts.Add($"Cases x{SlotMultiplier:0.#}");
            if (CadenceMultiplier < 1.0) parts.Add($"Cadence x{CadenceMultiplier:0.#}");
            return parts.Count == 0 ? "Aucune contrainte" : string.Join(" · ", parts).Replace(',', '.');
        }
    }
}

public static class Characters
{
    public static readonly CharacterDef Classic = new()
    {
        Id = "classic",
        Name = "Mocassins",
        Style = ShoeStyle.Derby,
        ShoeColor = new Color(0.62f, 0.33f, 0.15f),
        AccentColor = new Color(0.78f, 0.55f, 0.32f),
        PantsColor = new Color(0.26f, 0.26f, 0.34f),
    };

    public static readonly CharacterDef Lucky = new()
    {
        Id = "lucky",
        Name = "Baskets fétiches",
        Style = ShoeStyle.Sneaker,
        ShoeColor = new Color(0.95f, 0.25f, 0.55f),
        AccentColor = new Color(0.98f, 0.96f, 1f),
        PantsColor = new Color(0.2f, 0.26f, 0.45f),
        CostMultiplier = 2.0,
        JetonMultiplier = 3.0,
        UnlockRunEarnings = 1e7,
    };

    public static readonly CharacterDef HighRoller = new()
    {
        Id = "high_roller",
        Name = "Talons dorés",
        Style = ShoeStyle.Heel,
        ShoeColor = new Color(0.95f, 0.72f, 0.2f),
        AccentColor = new Color(0.55f, 0.08f, 0.12f),
        PantsColor = new Color(0.26f, 0.1f, 0.22f),
        CostMultiplier = 2.0,
        SlotMultiplier = 0.6,
        JetonMultiplier = 8.0,
        UnlockRunEarnings = 1e9,
    };

    public static readonly CharacterDef Cowboy = new()
    {
        Id = "cowboy",
        Name = "Santiags",
        Style = ShoeStyle.Boot,
        ShoeColor = new Color(0.66f, 0.38f, 0.18f),
        AccentColor = new Color(0.95f, 0.8f, 0.5f),
        PantsColor = new Color(0.18f, 0.28f, 0.48f),
        CostMultiplier = 3.0,
        SlotMultiplier = 0.6,
        CadenceMultiplier = 0.5,
        JetonMultiplier = 20.0,
        UnlockRunEarnings = 1e11,
    };

    public static readonly CharacterDef Beach = new()
    {
        Id = "beach",
        Name = "Tongs de plage",
        Style = ShoeStyle.FlipFlop,
        ShoeColor = new Color(0.2f, 0.85f, 0.95f),
        AccentColor = new Color(1f, 0.85f, 0.3f),
        PantsColor = new Color(0.95f, 0.45f, 0.3f),
        CostMultiplier = 4.0,
        SlotMultiplier = 0.5,
        CadenceMultiplier = 0.4,
        JetonMultiplier = 60.0,
        UnlockRunEarnings = 1e13,
    };

    public static readonly List<CharacterDef> All = new() { Classic, Lucky, HighRoller, Cowboy, Beach };

    public static CharacterDef ById(string id) => All.Find(c => c.Id == id) ?? Classic;
}
