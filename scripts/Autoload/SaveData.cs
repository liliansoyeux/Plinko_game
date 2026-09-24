using Godot;

namespace Plinko;

// Tiny persistent profile: best palier, best run score, last picked shoes, sound setting.
public static class SaveData
{
    private const string Path = "user://profile.cfg";
    private static ConfigFile _file;

    private static ConfigFile File
    {
        get
        {
            if (_file == null)
            {
                _file = new ConfigFile();
                _file.Load(Path);
            }
            return _file;
        }
    }

    public static int BestPalier => (int)File.GetValue("records", "best_palier", 0);
    public static float BestScore => (float)(double)File.GetValue("records", "best_score", 0.0);
    public static int RunsPlayed => (int)File.GetValue("records", "runs", 0);
    public static string LastCharacter => (string)File.GetValue("profile", "character", "classic");
    public static bool Muted => (bool)File.GetValue("profile", "muted", false);

    public static int Chips => (int)File.GetValue("meta", "chips", 0);

    public static void AddChips(int amount)
    {
        File.SetValue("meta", "chips", System.Math.Max(0, Chips + amount));
        Save();
    }

    public static int SkillLevel(string id) => (int)File.GetValue("skills", id, 0);

    public static void SetSkillLevel(string id, int level)
    {
        File.SetValue("skills", id, level);
        Save();
    }

    public static bool LastRunSetRecord { get; private set; }

    private static int _bestPalierAtRunStart;
    private static float _bestScoreAtRunStart;

    public static void BeginRun()
    {
        _bestPalierAtRunStart = BestPalier;
        _bestScoreAtRunStart = BestScore;
        LastRunSetRecord = false;
    }

    public static void RecordPalierReached(int palierNumber)
    {
        if (palierNumber > BestPalier)
        {
            File.SetValue("records", "best_palier", palierNumber);
            Save();
        }
    }

    public static void RecordRunEnd(int palierNumber, float totalScore)
    {
        LastRunSetRecord = palierNumber > _bestPalierAtRunStart || totalScore > _bestScoreAtRunStart;
        File.SetValue("records", "runs", RunsPlayed + 1);
        if (palierNumber > BestPalier)
        {
            File.SetValue("records", "best_palier", palierNumber);
        }
        if (totalScore > BestScore)
        {
            File.SetValue("records", "best_score", (double)totalScore);
        }
        Save();
    }

    public static void SetLastCharacter(string id)
    {
        File.SetValue("profile", "character", id);
        Save();
    }

    public static void SetMuted(bool muted)
    {
        File.SetValue("profile", "muted", muted);
        Save();
    }

    // Set by the debug autopilot so bot runs never overwrite the player's records.
    public static bool Disabled;

    private static void Save()
    {
        if (!Disabled)
        {
            File.Save(Path);
        }
    }
}
