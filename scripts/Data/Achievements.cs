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
        new() { Id = "balls_10", Name = "Poignée de billes", Description = "Posséder 10 billes.", Condition = m => m.TotalBalls >= 10 },
        new() { Id = "balls_100", Name = "Collectionneur", Description = "Posséder 100 billes.", Condition = m => m.TotalBalls >= 100 },
        new() { Id = "balls_500", Name = "Avalanche", Description = "Posséder 500 billes.", Condition = m => m.TotalBalls >= 500 },
        new() { Id = "tier_1", Name = "Argentier", Description = "Acheter une bille d'argent.", Condition = m => m.BallsOwned[1] > 0 },
        new() { Id = "tier_2", Name = "Chercheur d'or", Description = "Acheter une bille d'or.", Condition = m => m.BallsOwned[2] > 0 },
        new() { Id = "tier_3", Name = "Diamant brut", Description = "Acheter une bille de diamant.", Condition = m => m.BallsOwned[3] > 0 },
        new() { Id = "tier_4", Name = "Cœur de rubis", Description = "Acheter une bille de rubis.", Condition = m => m.BallsOwned[4] > 0 },
        new() { Id = "tier_5", Name = "Voyage cosmique", Description = "Acheter une bille cosmique.", Condition = m => m.BallsOwned[5] > 0 },
        new() { Id = "earn_1m", Name = "Millionnaire", Description = "Gagner 1M en une partie.", Condition = m => m.RunEarned >= 1e6 },
        new() { Id = "earn_1b", Name = "Milliardaire", Description = "Gagner 1B en une partie.", Condition = m => m.RunEarned >= 1e9 },
        new() { Id = "earn_1t", Name = "Hors catégorie", Description = "Gagner 1T en une partie.", Condition = m => m.RunEarned >= 1e12 },
        new() { Id = "rows_max", Name = "Plateau géant", Description = "Atteindre 16 rangées.", Condition = m => m.Rows >= 16 },
        new() { Id = "portal", Name = "Portier", Description = "Poser un portail dédoubleur.", Condition = m => m.PortalCells.Count > 0 },
        new() { Id = "frenzy", Name = "Frénétique", Description = "Déclencher une frénésie.", Condition = m => m.FrenzyTimeLeft > 0 },
        new() { Id = "prestige", Name = "Nouvelle paire", Description = "Changer de chaussures une première fois.", Condition = m => m.Prestiges > 0 },
        new() { Id = "all_shoes", Name = "Dressing complet", Description = "Débloquer les 5 paires de chaussures.", Condition = m => m.UnlockedShoes >= Characters.All.Count },
    };
}
