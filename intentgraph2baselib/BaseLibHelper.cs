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

    public static bool ShowMoveDetail
    {
        get => BaseConfig.ShowMoveDetail;
        set
        {
            BaseConfig.ShowMoveDetail = value;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.ShowMoveDetail));
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

    public static bool ShowCurrentMove
    {
        get => BaseConfig.ShowCurrentMove;
        set
        {
            BaseConfig.ShowCurrentMove = value;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.ShowCurrentMove));
        }
    }

    public static IntentGraphPosition IntentGraphPosition
    {
        get => BaseConfig.IntentGraphPosition;
        set
        {
            BaseConfig.IntentGraphPosition = value;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.IntentGraphPosition));
        }
    }

    [ConfigSlider(50, 150, 10, Format = "{0:0.##}%")]
    public static float IntentGraphScale
    {
        get => BaseConfig.IntentGraphScale * 100;
        set
        {
            BaseConfig.IntentGraphScale = value / 100;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.IntentGraphScale));
        }
    }

    [ConfigSection("Control")]
    public static bool PinableIntentGraph
    {
        get => BaseConfig.PinableIntentGraph;
        set
        {
            BaseConfig.PinableIntentGraph = value;
            BaseConfig.NotifyUpdated(nameof(IntentGraphModConfig.PinableIntentGraph));
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
