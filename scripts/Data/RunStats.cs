namespace Plinko;

// Every tweakable number of a run. Upgrades, maluses, characters and cocktails all just
// write into this; the board and RunManager read it.
public class RunStats
{
    public const int MaxRows = 16;

    public int RowCount = 8;
    public int BlockerCount = 0;       // wild blockers, placed at random (malus / bosses)
    public int PlacedBlockerCount = 0; // blockers the player positioned
    public int PortalCount = 0;
    public int ExtraFreeBalls = 0;
    public int BonusBallsPerPalier = 0;
    public float BallRadius = 9f;
    public float GlobalMultiplierModifier = 1f;
    public float SlotWidthModifier = 1f;
    public float NegativePenaltyReduction = 0f;
    public float GoldenBallChance = 0f;
    public float GoldenMultiplier = 2f;
    public float PegHitScore = 0f;
    public float XpMultiplier = 1f;
    public float MalusXpMultiplier = 1f;
    public float ChestLuck = 0f;
    public float JackpotBonus = 0f;   // extra payout on x10+ slots (skill tree)
    public int Rerolls = 0;           // chest rerolls available this run (skill tree)
    public int ChestChoices = 3;      // cards offered per bonus chest

    public RunStats Clone() => (RunStats)MemberwiseClone();
}
