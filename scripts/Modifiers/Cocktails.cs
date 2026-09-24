using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

// Cocktails: permanent passive bonuses, one glass on the machine's shelf each. Earned from
// the epic "Cocktail surprise" upgrade (or the beach flip-flops' starting perk).
public static class Cocktails
{
    // Factories rather than shared instances, so per-run state (like Mojito's "first ball"
    // flag) can never leak from one run into the next.
    private static readonly Func<IRunModifier>[] Factories =
    {
        () => new Mojito(),
        () => new TequilaSunrise(),
        () => new BlueLagoon(),
        () => new PinaColada(),
        () => new Negroni(),
        () => new Spritz(),
        () => new Cosmopolitan(),
        () => new OldFashioned(),
    };

    public static int Count => Factories.Length;

    public static IRunModifier PickUnused()
    {
        var active = RunManager.Instance.ActiveModifiers;
        var candidates = new List<IRunModifier>();
        foreach (var factory in Factories)
        {
            var cocktail = factory();
            if (!active.Exists(m => m.Id == cocktail.Id))
            {
                candidates.Add(cocktail);
            }
        }
        return candidates.Count == 0 ? null : candidates[new Random().Next(candidates.Count)];
    }
}

public class Mojito : IRunModifier
{
    public string Id => "mojito";
    public string DisplayName => "Mojito";
    public string Description => "La première bille de chaque palier est dorée.";
    public Color LiquidColor => new(0.55f, 1f, 0.45f);

    private bool _pending;

    public void OnPalierStart(PalierDef palier) => _pending = true;

    public void OnBallSpawn(Ball ball, RunStats stats)
    {
        if (_pending)
        {
            ball.IsGolden = true;
            _pending = false;
        }
    }
}

public class TequilaSunrise : IRunModifier
{
    public string Id => "tequila_sunrise";
    public string DisplayName => "Tequila Sunrise";
    public string Description => "Les cases x1 rapportent x3.";
    public Color LiquidColor => new(1f, 0.55f, 0.15f);

    public float ModifyPayout(Ball ball, float slotMultiplier, float payout)
        => Mathf.IsEqualApprox(slotMultiplier, 1f) ? payout * 3f : payout;
}

public class BlueLagoon : IRunModifier
{
    public string Id => "blue_lagoon";
    public string DisplayName => "Blue Lagoon";
    public string Description => "Les jackpots (x10 et plus) rapportent +30%.";
    public Color LiquidColor => new(0.2f, 0.65f, 1f);

    public float ModifyPayout(Ball ball, float slotMultiplier, float payout)
        => slotMultiplier >= 10f ? payout * 1.3f : payout;
}

public class PinaColada : IRunModifier
{
    public string Id => "pina_colada";
    public string DisplayName => "Piña Colada";
    public string Description => "Chaque clou touché rapporte +0,4 point.";
    public Color LiquidColor => new(1f, 0.95f, 0.75f);

    public void OnAcquired(RunStats stats) => stats.PegHitScore += 0.4f;
}

public class Negroni : IRunModifier
{
    public string Id => "negroni";
    public string DisplayName => "Negroni";
    public string Description => "+2 billes à chaque palier (dès maintenant).";
    public Color LiquidColor => new(0.9f, 0.15f, 0.2f);

    public void OnAcquired(RunStats stats)
    {
        stats.BonusBallsPerPalier += 2;
        RunManager.Instance.AddBalls(2);
    }
}

public class Spritz : IRunModifier
{
    public string Id => "spritz";
    public string DisplayName => "Spritz";
    public string Description => "Chaque niveau gagné offre une bille en plus (3 max par palier).";
    public Color LiquidColor => new(1f, 0.45f, 0.25f);

    // Capped: one big jackpot can grant several levels at once, and uncapped that turned into
    // a runaway loop (more balls -> more jackpots -> more levels -> more balls).
    private const int MaxPerPalier = 3;
    private int _givenThisPalier;

    public void OnPalierStart(PalierDef palier) => _givenThisPalier = 0;

    public void OnLevelUp(int newLevel)
    {
        if (_givenThisPalier < MaxPerPalier)
        {
            _givenThisPalier++;
            RunManager.Instance.AddBalls(1);
        }
    }
}

public class Cosmopolitan : IRunModifier
{
    public string Id => "cosmopolitan";
    public string DisplayName => "Cosmopolitan";
    public string Description => "La jauge de malus se remplit 40% moins vite.";
    public Color LiquidColor => new(1f, 0.3f, 0.6f);

    public void OnAcquired(RunStats stats) => stats.MalusXpMultiplier *= 0.6f;
}

public class OldFashioned : IRunModifier
{
    public string Id => "old_fashioned";
    public string DisplayName => "Old Fashioned";
    public string Description => "Billes dorées x3 au lieu de x2, et +8% de chance d'en avoir.";
    public Color LiquidColor => new(0.85f, 0.5f, 0.15f);

    public void OnAcquired(RunStats stats)
    {
        stats.GoldenMultiplier = 3f;
        stats.GoldenBallChance = Mathf.Min(1f, stats.GoldenBallChance + 0.08f);
    }
}
