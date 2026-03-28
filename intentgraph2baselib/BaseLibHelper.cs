using BaseLib.Config;
using Godot;
using IntentGraph2.Crossovers;

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

    [ConfigSection("Hotkey")]
    public static Key ToggleIntentGraphKey { get => BaseConfig.ToggleIntentGraphKey; set => BaseConfig.ToggleIntentGraphKey = value; }
}
