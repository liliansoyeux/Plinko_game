using Godot;
using System;
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

// "Your shoes are your character": each pair is a starting perk, and it's what you see
// resting on the machine for the whole run.
public class CharacterDef
{
    public string Id;
    public string Name;
    public string Perk;
    public ShoeStyle Style;
    public Color ShoeColor;
    public Color AccentColor;
    public Color PantsColor;
    public Action<RunStats> StatModifier = _ => { };
    public Action<RunManager> OnRunStart = _ => { };
}

public static class Characters
{
    public static readonly CharacterDef Classic = new()
    {
        Id = "classic",
        Name = "Mocassins",
        Perk = "Aucun bonus.\nLe choix des puristes.",
        Style = ShoeStyle.Derby,
        ShoeColor = new Color(0.62f, 0.33f, 0.15f),
        AccentColor = new Color(0.78f, 0.55f, 0.32f),
        PantsColor = new Color(0.26f, 0.26f, 0.34f),
    };

    public static readonly CharacterDef Lucky = new()
    {
        Id = "lucky",
        Name = "Baskets fétiches",
        Perk = "Bouclier +20% : les mauvaises cases rapportent plus.",
        Style = ShoeStyle.Sneaker,
        ShoeColor = new Color(0.95f, 0.25f, 0.55f),
        AccentColor = new Color(0.98f, 0.96f, 1f),
        PantsColor = new Color(0.2f, 0.26f, 0.45f),
        StatModifier = s => s.NegativePenaltyReduction += 0.2f,
    };

    public static readonly CharacterDef HighRoller = new()
    {
        Id = "high_roller",
        Name = "Talons dorés",
        Perk = "Gains +30%, mais 2 billes de moins par palier.",
        Style = ShoeStyle.Heel,
        ShoeColor = new Color(0.95f, 0.72f, 0.2f),
        AccentColor = new Color(0.55f, 0.08f, 0.12f),
        PantsColor = new Color(0.26f, 0.1f, 0.22f),
        StatModifier = s =>
        {
            s.GlobalMultiplierModifier *= 1.3f;
            s.BonusBallsPerPalier -= 2;
        },
    };

    public static readonly CharacterDef Cowboy = new()
    {
        Id = "cowboy",
        Name = "Santiags",
        Perk = "+3 billes par palier, mais des billes plus grosses.",
        Style = ShoeStyle.Boot,
        ShoeColor = new Color(0.66f, 0.38f, 0.18f),
        AccentColor = new Color(0.95f, 0.8f, 0.5f),
        PantsColor = new Color(0.18f, 0.28f, 0.48f),
        StatModifier = s =>
        {
            s.BonusBallsPerPalier += 3;
            s.BallRadius *= 1.15f;
        },
    };

    public static readonly CharacterDef Beach = new()
    {
        Id = "beach",
        Name = "Tongs de plage",
        Perk = "Commence la partie avec un cocktail au hasard.",
        Style = ShoeStyle.FlipFlop,
        ShoeColor = new Color(0.2f, 0.85f, 0.95f),
        AccentColor = new Color(1f, 0.85f, 0.3f),
        PantsColor = new Color(0.95f, 0.45f, 0.3f),
        OnRunStart = run => run.AddCocktail(Cocktails.PickUnused()),
    };

    public static readonly List<CharacterDef> All = new() { Classic, Lucky, HighRoller, Cowboy, Beach };

    public static CharacterDef ById(string id) => All.Find(c => c.Id == id) ?? Classic;
}
