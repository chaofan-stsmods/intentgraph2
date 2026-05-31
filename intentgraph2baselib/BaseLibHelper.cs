using BaseLib.Config;
using Godot;
using IntentGraph2.Crossovers;
using IntentGraph2.Utils;

namespace IntentGraph2.BaseLib;

public class BaseLibHelper : IBaseLibHelper
{
    public IntentGraphModConfig Config => HelperModConfig.BaseConfig;

    public void RegisterConfig()
    {
        ModConfigRegistry.Register(IntentGraphMod.ModId, new HelperModConfig());
    }
}

public class HelperModConfig : SimpleModConfig
{
    internal static IntentGraphModConfig BaseConfig { get; set; } = new IntentGraphModConfig();

    [ConfigSection("Display")]
    public static bool ShowMonsterMoveNames { get => BaseConfig.ShowMonsterMoveNames; set => BaseConfig.ShowMonsterMoveNames = value; }

    [ConfigSection("Hotkey")]
    public static Key ToggleIntentGraphKey { get => BaseConfig.ToggleIntentGraphKey; set => BaseConfig.ToggleIntentGraphKey = value; }
}
