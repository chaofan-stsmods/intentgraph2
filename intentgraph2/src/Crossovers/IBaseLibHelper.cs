//using BaseLib.Config;
using Godot;

namespace IntentGraph2.Crossovers;

public interface IBaseLibHelper
{
    IntentGraphModConfig Config { get; }

    void RegisterConfig();
}

public class IntentGraphModConfig
{
    public Key ToggleIntentGraphKey { get; set; } = Key.F1;
}
