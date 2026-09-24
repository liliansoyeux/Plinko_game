using System;

namespace Plinko;

public class AchievementDef
{
    public string Id;
    public string Name;
    public string Description;
    public Func<IdleManager, bool> Condition;
}

// Milestones: each one unlocked gives +2% income forever (like Cookie Clicker's milk).
public static class Achievements
{
    public const double BonusPerAchievement = 0.02;

    public static readonly AchievementDef[] All =
    {
        new() { Id = "balls_100", Name = "Poignée de billes", Description = "Lâcher 100 billes.", Condition = m => m.LifetimeBallsDropped >= 100 },
        new() { Id = "balls_10k", Name = "Collectionneur", Description = "Lâcher 10 000 billes.", Condition = m => m.LifetimeBallsDropped >= 1e4 },
        new() { Id = "balls_1m", Name = "Avalanche", Description = "Lâcher 1 million de billes.", Condition = m => m.LifetimeBallsDropped >= 1e6 },
        new() { Id = "tier_1", Name = "Argentier", Description = "Débloquer les billes d'argent.", Condition = m => m.TiersUnlocked > 1 },
        new() { Id = "tier_2", Name = "Chercheur d'or", Description = "Débloquer les billes d'or.", Condition = m => m.TiersUnlocked > 2 },
        new() { Id = "tier_3", Name = "Diamant brut", Description = "Débloquer les billes de diamant.", Condition = m => m.TiersUnlocked > 3 },
        new() { Id = "tier_4", Name = "Cœur de rubis", Description = "Débloquer les billes de rubis.", Condition = m => m.TiersUnlocked > 4 },
        new() { Id = "tier_5", Name = "Voyage cosmique", Description = "Débloquer les billes cosmiques.", Condition = m => m.TiersUnlocked > 5 },
        new() { Id = "earn_1m", Name = "Millionnaire", Description = "Gagner 1M en une partie.", Condition = m => m.RunEarned >= 1e6 },
        new() { Id = "earn_1b", Name = "Milliardaire", Description = "Gagner 1B en une partie.", Condition = m => m.RunEarned >= 1e9 },
        new() { Id = "earn_1t", Name = "Hors catégorie", Description = "Gagner 1T en une partie.", Condition = m => m.RunEarned >= 1e12 },
        new() { Id = "portal", Name = "Portier", Description = "Poser un portail dédoubleur.", Condition = m => m.PortalCells.Count > 0 },
        new() { Id = "frenzy", Name = "Frénétique", Description = "Déclencher une frénésie.", Condition = m => m.FrenzyTimeLeft > 0 },
        new() { Id = "prestige", Name = "Nouvelle paire", Description = "Changer de chaussures une première fois.", Condition = m => m.Prestiges > 0 },
        new() { Id = "all_shoes", Name = "Dressing complet", Description = "Débloquer les 5 paires de chaussures.", Condition = m => m.UnlockedShoes >= Characters.All.Count },
    };
}
