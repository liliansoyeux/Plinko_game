using Godot;

namespace Plinko;

// Thin wrapper around the save file (a ConfigFile). IdleManager owns what goes in it.
public static class SaveData
{
    private const string Path = "user://idle_save.cfg";
    private static ConfigFile _file;

    // Set by the debug autopilot so bot sessions never overwrite the player's save.
    public static bool Disabled;

    public static ConfigFile File
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

    public static bool Muted
    {
        get => (bool)File.GetValue("settings", "muted", false);
        set
        {
            File.SetValue("settings", "muted", value);
            Save();
        }
    }

    public static void Save()
    {
        if (!Disabled)
        {
            File.Save(Path);
        }
    }

    public static void Wipe()
    {
        bool muted = Muted;
        _file = new ConfigFile();
        _file.SetValue("settings", "muted", muted);
        Save();
    }
}
