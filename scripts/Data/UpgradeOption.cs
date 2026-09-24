using System;
using System.Collections.Generic;

namespace Plinko;

public enum UpgradeIcon
{
    Rows,
    SmallBall,
    BigBall,
    Multiplier,
    MultiplierDown,
    TwinBall,
    ExtraBalls,
    FewerBalls,
    Blocker,
    Shield,
    ShieldBroken,
    GoldBall,
    Peg,
    Xp,
    Chest,
    Star,
    Cocktail,
    NarrowSlots,
    Skull,
    Portal,
    Reroll,
    Cards,
    Heart,
}

public class UpgradeOption
{
    public string Id;
    public string Label;
    public string Description;
    public Rarity Rarity;
    public UpgradeIcon Icon;
    public Action<RunStats> Apply;
    public Func<RunStats, bool> CanOffer;
    public int MaxStacks;

    public UpgradeOption(string id, string label, string description, Rarity rarity, UpgradeIcon icon,
        Action<RunStats> apply, Func<RunStats, bool> canOffer = null, int maxStacks = 0)
    {
        Id = id;
        Label = label;
        Description = description;
        Rarity = rarity;
        Icon = icon;
        Apply = apply;
        CanOffer = canOffer;
        MaxStacks = maxStacks;
    }

    public bool IsAvailable(RunStats stats, RunRecord record)
    {
        if (MaxStacks > 0 && record.UpgradeCounts.TryGetValue(Id, out int taken) && taken >= MaxStacks)
        {
            return false;
        }
        return CanOffer == null || CanOffer(stats);
    }
}

public static class UpgradeCatalog
{
    public static readonly UpgradeOption[] All =
    {
        // ---- Commun --------------------------------------------------------------------
        new("row_plus_1", "+1 Rangée", "Ajoute une rangée de clous et une case de plus en bas.",
            Rarity.Common, UpgradeIcon.Rows, s => s.RowCount++,
            s => s.RowCount < RunStats.MaxRows),

        new("ball_smaller", "Bille affinée", "Billes 12% plus petites : elles se faufilent mieux.",
            Rarity.Common, UpgradeIcon.SmallBall, s => s.BallRadius = MathF.Max(5f, s.BallRadius * 0.88f),
            s => s.BallRadius > 5.5f),

        new("global_multiplier", "Multiplicateur +10%", "Tous les gains augmentent de 10%.",
            Rarity.Common, UpgradeIcon.Multiplier, s => s.GlobalMultiplierModifier *= 1.1f),

        new("extra_balls", "+2 Billes", "Deux billes de plus au début de chaque palier.",
            Rarity.Common, UpgradeIcon.ExtraBalls, s => s.BonusBallsPerPalier += 2),

        new("blocker_plus_1", "Bâton à placer", "Pose un bâton où tu veux sur le plateau pour guider tes billes.",
            Rarity.Common, UpgradeIcon.Blocker,
            s =>
            {
                s.PlacedBlockerCount++;
                RunManager.Instance.RequestBoardAction(BoardAction.PlaceBlocker);
            },
            s => s.BlockerCount + s.PlacedBlockerCount < 10),

        new("peg_score", "Clous payants", "Chaque clou touché rapporte +0,25 point.",
            Rarity.Common, UpgradeIcon.Peg, s => s.PegHitScore += 0.25f),

        // ---- Rare ----------------------------------------------------------------------
        new("negative_shield", "Bouclier +20%", "Les mauvaises cases (x0.x) rapportent 20% de plus.",
            Rarity.Rare, UpgradeIcon.Shield,
            s => s.NegativePenaltyReduction = MathF.Min(1f, s.NegativePenaltyReduction + 0.2f),
            s => s.NegativePenaltyReduction < 0.9f),

        new("twin_ball", "Bille jumelle", "Chaque lâcher libère une bille bonus gratuite.",
            Rarity.Rare, UpgradeIcon.TwinBall, s => s.ExtraFreeBalls++, maxStacks: 2),

        new("golden_chance", "Pluie d'or", "+12% de chances qu'une bille soit dorée (gains x2).",
            Rarity.Rare, UpgradeIcon.GoldBall, s => s.GoldenBallChance = MathF.Min(1f, s.GoldenBallChance + 0.12f),
            s => s.GoldenBallChance < 0.9f),

        new("xp_boost", "Expérience +30%", "Tu montes de niveau plus vite : plus de coffres.",
            Rarity.Rare, UpgradeIcon.Xp, s => s.XpMultiplier *= 1.3f),

        new("chest_luck", "Coffres chanceux", "Les coffres rares et épiques apparaissent plus souvent.",
            Rarity.Rare, UpgradeIcon.Chest, s => s.ChestLuck += 0.12f, maxStacks: 3),

        // ---- Épique --------------------------------------------------------------------
        new("replace_worst_slot", "Case réhabilitée", "La pire case du plateau devient une bonne case.",
            Rarity.Epic, UpgradeIcon.Star,
            s => RunManager.Instance.RequestBoardAction(BoardAction.ReplaceWorstSlot)),

        new("boost_best_slot", "Super jackpot", "La meilleure case du plateau voit son multiplicateur x1,5.",
            Rarity.Epic, UpgradeIcon.Star,
            s => RunManager.Instance.RequestBoardAction(BoardAction.BoostBestSlot)),

        new("cocktail_drop", "Cocktail surprise", "Un nouveau cocktail (bonus passif permanent) sur la machine.",
            Rarity.Epic, UpgradeIcon.Cocktail,
            s => RunManager.Instance.AddCocktail(Cocktails.PickUnused()),
            s => Cocktails.PickUnused() != null),

        new("big_multiplier", "Multiplicateur +35%", "Tous les gains augmentent de 35%.",
            Rarity.Epic, UpgradeIcon.Multiplier, s => s.GlobalMultiplierModifier *= 1.35f),

        // ---- Légendaire (ultra rare) ----------------------------------------------------
        new("portal", "Portail dédoubleur", "Place un portail : chaque bille qui le traverse se dédouble !",
            Rarity.Legendary, UpgradeIcon.Portal,
            s =>
            {
                s.PortalCount++;
                RunManager.Instance.RequestBoardAction(BoardAction.PlacePortal);
            },
            maxStacks: 3),
    };

    public static List<UpgradeOption> PickForChest(Rarity rarity, int count, RunStats stats, RunRecord record)
    {
        // The chest's rarity is guaranteed for the first card; the others come from that
        // rarity or one tier below, so an epic chest never offers three filler picks.
        var available = Array.FindAll(All, o => o.IsAvailable(stats, record));
        var primary = new List<UpgradeOption>(Array.FindAll(available, o => o.Rarity == rarity));
        var secondary = new List<UpgradeOption>(Array.FindAll(available, o => o.Rarity == rarity || o.Rarity == rarity - 1));
        var rest = new List<UpgradeOption>(available);

        var rng = new Random();
        var picked = new List<UpgradeOption>();
        TakeRandom(primary, picked, 1, rng);
        TakeRandom(secondary, picked, count, rng);
        TakeRandom(rest, picked, count, rng);
        return picked;
    }

    public static void TakeRandom(List<UpgradeOption> pool, List<UpgradeOption> picked, int targetCount, Random rng)
    {
        pool.RemoveAll(picked.Contains);
        while (picked.Count < targetCount && pool.Count > 0)
        {
            int index = rng.Next(pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }
}
