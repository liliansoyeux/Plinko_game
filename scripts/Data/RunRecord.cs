using System.Collections.Generic;

namespace Plinko;

// What happened during a run — shown on the game-over screen and in the pause menu.
public class RunRecord
{
    public float TotalScore;
    public float BestHitPayout;
    public float BestHitMultiplier;
    public int BallsDropped;
    public int PegHits;
    public int Jackpots;
    public int ChestsOpened;
    public int CursedChestsOpened;
    public int PaliersCleared;
    public int BossesBeaten;
    public bool SecondChanceUsed;
    public int ChipsEarned = -1; // -1 until the run's reward has been paid out
    public readonly Dictionary<string, int> UpgradeCounts = new();
    public readonly List<UpgradeOption> UpgradesInOrder = new();
}
