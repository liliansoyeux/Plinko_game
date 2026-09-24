using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

public readonly record struct SlotHit(Ball Ball, float Multiplier, float Payout, bool IsBad, bool IsMiss);

// Owns the rules of a run: paliers, score, XP/levels, malus gauge, stats and cocktails.
// Knows nothing about nodes on screen — the board and UI listen to its events.
public partial class RunManager : Node
{
    public static RunManager Instance { get; private set; }

    public event Action<float> ScoreChanged;
    public event Action<float, float> XpChanged;
    public event Action<int> LeveledUp;
    public event Action ChestSpawnRequested;
    public event Action<float, float> NegativeXpChanged;
    public event Action<int> MalusTriggered;
    public event Action MalusChestSpawnRequested;
    public event Action StatsChanged;
    public event Action<int> BallsRemainingChanged;
    public event Action<PalierDef> PalierStarted;
    public event Action<PalierDef> PalierCleared;
    public event Action<PalierDef> GameOver;
    public event Action<SlotHit> SlotScored;
    public event Action<BoardAction> BoardActionRequested;
    public event Action<IRunModifier> CocktailAdded;
    public event Action SecondChanceUsed;

    private const float XpPerMultiplierPoint = 2f;
    private const float LevelXpGrowth = 1.18f;
    private const float NegativeXpPerBadness = 9f;
    private const float MissNegativeXp = 12f;
    private const float MalusXpGrowth = 1.2f;
    private const float FirstLevelXp = 60f;
    private const float FirstMalusXp = 80f;

    public GameState State { get; private set; } = GameState.Idle;
    public RunStats Stats { get; private set; } = new();
    public RunRecord Record { get; private set; } = new();
    public float Score { get; private set; }

    public int Level { get; private set; } = 1;
    public float Xp { get; private set; }
    public float XpToNextLevel { get; private set; } = FirstLevelXp;

    public int MalusLevel { get; private set; }
    public float NegativeXp { get; private set; }
    public float NegativeXpToNext { get; private set; } = FirstMalusXp;

    public int PalierIndex { get; private set; } = -1;
    public PalierDef CurrentPalier { get; private set; }
    public int BallsRemaining { get; private set; }
    public int BallsInPlay { get; private set; }

    // Bumped on every new run. Balls remember the run they were spawned in, so a ball freed
    // while tearing down an old run can't decrement the new run's in-play counter.
    public int RunId { get; private set; }

    public List<IRunModifier> ActiveModifiers { get; } = new();
    public CharacterDef SelectedCharacter { get; private set; } = Characters.Classic;

    public bool IsPlaying => State == GameState.Playing;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void StartNewRun(CharacterDef character)
    {
        RunId++;
        SelectedCharacter = character;
        Stats = new RunStats();
        Record = new RunRecord();
        ActiveModifiers.Clear();
        character.StatModifier(Stats);
        SkillTree.ApplyToStats(Stats);

        Level = 1;
        Xp = 0f;
        XpToNextLevel = FirstLevelXp;
        MalusLevel = 0;
        NegativeXp = 0f;
        NegativeXpToNext = FirstMalusXp;
        PalierIndex = -1;
        Score = 0f;

        SaveData.BeginRun();
        character.OnRunStart(this);

        StatsChanged?.Invoke();
        XpChanged?.Invoke(Xp, XpToNextLevel);
        LeveledUp?.Invoke(Level);
        NegativeXpChanged?.Invoke(NegativeXp, NegativeXpToNext);
        MalusTriggered?.Invoke(MalusLevel);

        StartNextPalier();
        GD.Print($"[Run] #{RunId} started with {character.Name} (skills: {SkillTree.SpentChips()} chips, cards {Stats.ChestChoices}, rerolls {Stats.Rerolls})");
    }

    public void AbandonRun()
    {
        // Leaving mid-run still pays for the progress made, so restarting isn't punished.
        if (State is GameState.Playing or GameState.PalierCleared)
        {
            PayRunReward();
        }
        State = GameState.Idle;
        RunId++;
    }

    public void StartNextPalier()
    {
        PalierIndex++;
        CurrentPalier = PalierGenerator.Generate(PalierIndex);
        BallsRemaining = Math.Max(3, CurrentPalier.BallCount + Stats.BonusBallsPerPalier);
        BallsInPlay = 0;
        Score = 0f;
        State = GameState.Playing;

        foreach (var modifier in ActiveModifiers)
        {
            modifier.OnPalierStart(CurrentPalier);
        }

        ScoreChanged?.Invoke(Score);
        BallsRemainingChanged?.Invoke(BallsRemaining);
        PalierStarted?.Invoke(CurrentPalier);
        GD.Print($"[Palier] {PalierIndex + 1} start (target {CurrentPalier.ScoreTarget:0}, balls {BallsRemaining}, boss={CurrentPalier.IsBoss})");
    }

    // Debug/autopilot only: tweak stats directly (e.g. max rows) and refresh the board.
    public void DebugModifyStats(Action<RunStats> change)
    {
        change(Stats);
        StatsChanged?.Invoke();
    }

    // Debug/autopilot only: jump straight to a later palier (e.g. to look at a boss).
    public void DebugJumpToPalier(int palierIndex)
    {
        PalierIndex = palierIndex - 1;
        StartNextPalier();
    }

    // Called by the UI once the "palier cleared" celebration has played out.
    public void AdvanceToNextPalier()
    {
        if (State == GameState.PalierCleared)
        {
            StartNextPalier();
        }
    }

    public bool TryConsumeBall()
    {
        if (State != GameState.Playing || BallsRemaining <= 0)
        {
            return false;
        }

        BallsRemaining--;
        BallsInPlay++;
        Record.BallsDropped++;
        BallsRemainingChanged?.Invoke(BallsRemaining);
        return true;
    }

    // Twin balls ride along with a consumed ball for free: tracked in play, never counted.
    public void RegisterFreeBall()
    {
        BallsInPlay++;
        Record.BallsDropped++;
    }

    public void AddBalls(int count)
    {
        if (State != GameState.Playing && State != GameState.PalierCleared)
        {
            return;
        }
        BallsRemaining += count;
        BallsRemainingChanged?.Invoke(BallsRemaining);
    }

    public void NotifyBallSettled(int runId)
    {
        if (runId != RunId)
        {
            return;
        }

        BallsInPlay = Math.Max(0, BallsInPlay - 1);

        if (State == GameState.Playing && BallsRemaining <= 0 && BallsInPlay <= 0)
        {
            EvaluatePalierEnd();
        }
    }

    private void EvaluatePalierEnd()
    {
        if (Score >= CurrentPalier.ScoreTarget)
        {
            State = GameState.PalierCleared;
            Record.PaliersCleared++;
            if (CurrentPalier.IsBoss)
            {
                Record.BossesBeaten++;
            }
            SaveData.RecordPalierReached(PalierIndex + 2);
            GD.Print($"[Palier] {PalierIndex + 1} cleared ({Score:0}/{CurrentPalier.ScoreTarget:0})");
            PalierCleared?.Invoke(CurrentPalier);
        }
        else if (SkillTree.Has("safe_second") && !Record.SecondChanceUsed && Score >= CurrentPalier.ScoreTarget * 0.6f)
        {
            Record.SecondChanceUsed = true;
            BallsRemaining += 3;
            BallsRemainingChanged?.Invoke(BallsRemaining);
            GD.Print($"[Palier] {PalierIndex + 1} second chance ({Score:0}/{CurrentPalier.ScoreTarget:0})");
            SecondChanceUsed?.Invoke();
        }
        else
        {
            State = GameState.GameOver;
            PayRunReward();
            SaveData.RecordRunEnd(PalierIndex + 1, Record.TotalScore);
            GD.Print($"[Palier] {PalierIndex + 1} failed ({Score:0}/{CurrentPalier.ScoreTarget:0}) -> game over");
            GameOver?.Invoke(CurrentPalier);
        }
    }

    private void PayRunReward()
    {
        if (Record.ChipsEarned >= 0)
        {
            return;
        }
        Record.ChipsEarned = SkillTree.RunReward(Record.PaliersCleared, Record.BossesBeaten, Level).total;
        SaveData.AddChips(Record.ChipsEarned);
        GD.Print($"[Run] reward {Record.ChipsEarned} chips");
    }

    public float ScoreSlot(Ball ball, float multiplier)
    {
        if (State != GameState.Playing)
        {
            return 0f;
        }

        bool isBad = multiplier > 0f && multiplier < 1f;
        float effective = isBad ? Mathf.Min(1f, multiplier + Stats.NegativePenaltyReduction) : multiplier;
        float payout = effective * Stats.GlobalMultiplierModifier;
        if (ball != null && ball.IsGolden)
        {
            payout *= Stats.GoldenMultiplier;
        }
        if (multiplier >= 10f)
        {
            payout *= 1f + Stats.JackpotBonus;
        }
        foreach (var modifier in ActiveModifiers)
        {
            payout = modifier.ModifyPayout(ball, multiplier, payout);
        }

        AddScore(payout);
        if (payout > Record.BestHitPayout)
        {
            Record.BestHitPayout = payout;
            Record.BestHitMultiplier = multiplier;
        }
        if (multiplier >= 10f)
        {
            Record.Jackpots++;
        }

        SlotScored?.Invoke(new SlotHit(ball, multiplier, payout, isBad, false));

        foreach (var modifier in ActiveModifiers)
        {
            modifier.OnSlotHit(ball, multiplier);
        }

        if (multiplier > 1f)
        {
            AddXp(multiplier * XpPerMultiplierPoint * Stats.XpMultiplier);
        }
        else if (isBad)
        {
            // Reciprocal scale so the worst slots (x0.1) hit hard, mirroring how the biggest
            // jackpots dominate the positive XP side.
            float badness = (1f / multiplier) - 1f;
            AddNegativeXp(badness * NegativeXpPerBadness * Stats.MalusXpMultiplier);
        }

        return payout;
    }

    // A ball that slipped between narrowed slots: pays nothing and feeds the malus gauge.
    public void RegisterMiss(Ball ball)
    {
        if (State != GameState.Playing)
        {
            return;
        }
        SlotScored?.Invoke(new SlotHit(ball, 0f, 0f, true, true));
        AddNegativeXp(MissNegativeXp * Stats.MalusXpMultiplier);
    }

    public void RegisterPegHit(Ball ball)
    {
        if (State != GameState.Playing)
        {
            return;
        }
        Record.PegHits++;
        if (Stats.PegHitScore > 0f)
        {
            AddScore(Stats.PegHitScore);
        }
        foreach (var modifier in ActiveModifiers)
        {
            modifier.OnPegHit(ball);
        }
    }

    private void AddScore(float amount)
    {
        Score += amount;
        Record.TotalScore += amount;
        ScoreChanged?.Invoke(Score);
    }

    private void AddXp(float amount)
    {
        Xp += amount;
        while (Xp >= XpToNextLevel)
        {
            Xp -= XpToNextLevel;
            Level++;
            XpToNextLevel *= LevelXpGrowth;
            LeveledUp?.Invoke(Level);
            ChestSpawnRequested?.Invoke();
            foreach (var modifier in ActiveModifiers)
            {
                modifier.OnLevelUp(Level);
            }
        }
        XpChanged?.Invoke(Xp, XpToNextLevel);
    }

    private void AddNegativeXp(float amount)
    {
        NegativeXp += amount;
        while (NegativeXp >= NegativeXpToNext)
        {
            NegativeXp -= NegativeXpToNext;
            MalusLevel++;
            NegativeXpToNext *= MalusXpGrowth;
            MalusTriggered?.Invoke(MalusLevel);
            MalusChestSpawnRequested?.Invoke();
        }
        NegativeXpChanged?.Invoke(NegativeXp, NegativeXpToNext);
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
        option.Apply(Stats);
        Record.UpgradeCounts[option.Id] = Record.UpgradeCounts.GetValueOrDefault(option.Id) + 1;
        Record.UpgradesInOrder.Add(option);
        if (option.Rarity == Rarity.Cursed)
        {
            Record.CursedChestsOpened++;
        }
        else
        {
            Record.ChestsOpened++;
        }
        StatsChanged?.Invoke();
        GD.Print($"[Upgrade] applied {option.Id}");
    }

    public void AddCocktail(IRunModifier cocktail)
    {
        if (cocktail == null || ActiveModifiers.Exists(m => m.Id == cocktail.Id))
        {
            return;
        }
        ActiveModifiers.Add(cocktail);
        cocktail.OnAcquired(Stats);
        CocktailAdded?.Invoke(cocktail);
        StatsChanged?.Invoke();
        GD.Print($"[Cocktail] {cocktail.DisplayName}");
    }

    public void RequestBoardAction(BoardAction action) => BoardActionRequested?.Invoke(action);

    public void NotifyBallSpawned(Ball ball)
    {
        if (Stats.GoldenBallChance > 0f && GD.Randf() < Stats.GoldenBallChance)
        {
            ball.IsGolden = true;
        }
        foreach (var modifier in ActiveModifiers)
        {
            modifier.OnBallSpawn(ball, Stats);
        }
    }
}
