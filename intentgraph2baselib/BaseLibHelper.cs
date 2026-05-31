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

    public void SaveConfig()
    {
        ModConfig.SaveDebounced<HelperModConfig>();
    }
}

public class HelperModConfig : SimpleModConfig
{
    internal static IntentGraphModConfig BaseConfig { get; } = new IntentGraphModConfig();

    [ConfigSection("Display")]
    public static bool ShowMonsterMoveNames
    {
        get => BaseConfig.ShowMonsterMoveNames;
        set
        {
            BaseConfig.ShowMonsterMoveNames = value;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.ShowMonsterMoveNames));
        }
    }

    public static bool UseAnimatedIntentIcon
    {
        get => BaseConfig.UseAnimatedIntentIcon;
        set
        {
            BaseConfig.UseAnimatedIntentIcon = value;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.UseAnimatedIntentIcon));
        }
    }

    [ConfigSection("Hotkey")]
    public static Key ToggleIntentGraphKey
    {
        get => BaseConfig.ToggleIntentGraphKey;
        set
        {
            BaseConfig.ToggleIntentGraphKey = value;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.ToggleIntentGraphKey));
        }
    }
}
