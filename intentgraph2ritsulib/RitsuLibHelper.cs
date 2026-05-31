using Godot;
using IntentGraph2.Crossovers;
using IntentGraph2.Utils;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace IntentGraph2.RitsuLib;

public class RitsuLibHelper : IRitsuLibHelper
{
    private const string SettingKey = "Settings";

    public IntentGraphModConfig Config => RitsuLibFramework.GetDataStore(IntentGraphMod.ModId).Get<IntentGraphModConfig>(SettingKey);

    public void RegisterConfig()
    {
        using (RitsuLibFramework.BeginModDataRegistration(IntentGraphMod.ModId))
        {
            var store = RitsuLibFramework.GetDataStore(IntentGraphMod.ModId);
            store.Register(
                key: SettingKey,
                fileName: "settings.json",
                scope: SaveScope.Global,
                defaultFactory: () => new IntentGraphModConfig(),
                autoCreateIfMissing: true);
        }

        var toggleIntentGraph = new ModSettingsValueBinding<IntentGraphModConfig, Key>(
            IntentGraphMod.ModId,
            SettingKey,
            SaveScope.Global,
            settings => settings.ToggleIntentGraphKey,
            (settings, value) => settings.ToggleIntentGraphKey = value);

        RitsuLibFramework.RegisterModSettings(IntentGraphMod.ModId, page =>
        {
            page.WithTitle(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2.mod_title", "Intent Graph"));
            page.WithDescription(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2.mod_description", "Show monster intent as a state machine."));
            page.WithModDisplayName(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2.mod_title", "Intent Graph"));
            page.AddSection("hotkey", section =>
            {
                section.WithTitle(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-HOTKEY.title", "Hotkey"));
                section.AddEnumChoice(
                    "toggle_intent_graph",
                    ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-TOGGLE_INTENT_GRAPH_KEY.title", "Toggle Intent Graph"),
                    toggleIntentGraph,
                    presentation: ModSettingsChoicePresentation.Dropdown);
            });
        });
    }
}
