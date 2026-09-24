using System;
using System.Collections.Generic;

namespace Plinko;

// Cursed chests offer three of these: the player still picks, but only the lesser evil.
public static class MalusCatalog
{
    public static readonly UpgradeOption[] All =
    {
        new("ball_bigger", "Bille enflée", "Billes 15% plus grosses.",
            Rarity.Cursed, UpgradeIcon.BigBall, s => s.BallRadius = MathF.Min(16f, s.BallRadius * 1.15f),
            s => s.BallRadius < 15f),

        new("multiplier_down", "Gains -10%", "Tous les gains baissent de 10%.",
            Rarity.Cursed, UpgradeIcon.MultiplierDown, s => s.GlobalMultiplierModifier *= 0.9f),

        new("slot_narrower", "Cases -10%", "Les cases rétrécissent : des billes tomberont entre elles.",
            Rarity.Cursed, UpgradeIcon.NarrowSlots, s => s.SlotWidthModifier *= 0.9f,
            s => s.SlotWidthModifier > 0.6f),

        new("shield_down", "Bouclier -20%", "Les mauvaises cases (x0.x) rapportent encore moins.",
            Rarity.Cursed, UpgradeIcon.ShieldBroken,
            s => s.NegativePenaltyReduction = MathF.Max(0f, s.NegativePenaltyReduction - 0.2f),
            s => s.NegativePenaltyReduction > 0.05f),

        new("fewer_balls", "-1 Bille", "Une bille de moins à chaque palier.",
            Rarity.Cursed, UpgradeIcon.FewerBalls, s => s.BonusBallsPerPalier--,
            s => s.BonusBallsPerPalier > -4),

        new("curse_good_slot", "Case maudite", "Une bonne case du plateau devient x0.5.",
            Rarity.Cursed, UpgradeIcon.Skull,
            s => RunManager.Instance.RequestBoardAction(BoardAction.CurseGoodSlot)),

        new("rogue_blocker", "Bâton sauvage", "Un bâton apparaît au hasard sur le plateau, sans que tu choisisses où.",
            Rarity.Cursed, UpgradeIcon.Blocker, s => s.BlockerCount++,
            s => s.BlockerCount + s.PlacedBlockerCount < 10),
    };

    public static List<UpgradeOption> PickRandom(int count, RunStats stats, RunRecord record)
    {
        var pool = new List<UpgradeOption>(Array.FindAll(All, o => o.IsAvailable(stats, record)));
        var picked = new List<UpgradeOption>();
        UpgradeCatalog.TakeRandom(pool, picked, count, new Random());
        return picked;
    }
}
