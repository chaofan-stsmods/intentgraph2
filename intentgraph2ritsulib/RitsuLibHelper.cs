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
            (settings, value) =>
            {
                settings.ToggleIntentGraphKey = value;
                settings.NotifyUpdated(nameof(IntentGraphModConfig.ToggleIntentGraphKey));
            });

        var showMonsterMoveNames = new ModSettingsValueBinding<IntentGraphModConfig, bool>(
            IntentGraphMod.ModId,
            SettingKey,
            SaveScope.Global,
            settings => settings.ShowMonsterMoveNames,
            (settings, value) =>
            {
                settings.ShowMonsterMoveNames = value;
                settings.NotifyUpdated(nameof(IntentGraphModConfig.ShowMonsterMoveNames));
            });

        var useAnimatedIntentIcon = new ModSettingsValueBinding<IntentGraphModConfig, bool>(
            IntentGraphMod.ModId,
            SettingKey,
            SaveScope.Global,
            settings => settings.UseAnimatedIntentIcon,
            (settings, value) =>
            {
                settings.UseAnimatedIntentIcon = value;
                settings.NotifyUpdated(nameof(IntentGraphModConfig.UseAnimatedIntentIcon));
            });

        var showCurrentMove = new ModSettingsValueBinding<IntentGraphModConfig, bool>(
            IntentGraphMod.ModId,
            SettingKey,
            SaveScope.Global,
            settings => settings.ShowCurrentMove,
            (settings, value) =>
            {
                settings.ShowCurrentMove = value;
                settings.NotifyUpdated(nameof(IntentGraphModConfig.ShowCurrentMove));
            });

        var intentGraphPosition = new ModSettingsValueBinding<IntentGraphModConfig, IntentGraphPosition>(
            IntentGraphMod.ModId,
            SettingKey,
            SaveScope.Global,
            settings => settings.IntentGraphPosition,
            (settings, value) =>
            {
                settings.IntentGraphPosition = value;
                settings.NotifyUpdated(nameof(IntentGraphModConfig.IntentGraphPosition));
            });

        var intentGraphScale = new ModSettingsValueBinding<IntentGraphModConfig, double>(
            IntentGraphMod.ModId,
            SettingKey,
            SaveScope.Global,
            settings => settings.IntentGraphScale * 100,
            (settings, value) =>
            {
                settings.IntentGraphScale = (float)value / 100;
                settings.NotifyUpdated(nameof(IntentGraphModConfig.IntentGraphScale));
            });

        var pinableIntentGraph = new ModSettingsValueBinding<IntentGraphModConfig, bool>(
            IntentGraphMod.ModId,
            SettingKey,
            SaveScope.Global,
            settings => settings.PinableIntentGraph,
            (settings, value) =>
            {
                settings.PinableIntentGraph = value;
                settings.NotifyUpdated(nameof(IntentGraphModConfig.PinableIntentGraph));
            });

        RitsuLibFramework.RegisterModSettings(IntentGraphMod.ModId, page =>
        {
            page.WithTitle(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2.mod_title", "Intent Graph"));
            page.WithDescription(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2.mod_description", "Show monster intent as a state machine."));
            page.WithModDisplayName(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2.mod_title", "Intent Graph"));
            page.AddSection("display", section =>
            {
                section.WithTitle(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-DISPLAY.title", "Display"));
                section.AddToggle(
                    "show_monster_move_names",
                    ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-SHOW_MONSTER_MOVE_NAMES.title", "Show Monster Move Names"),
                    showMonsterMoveNames);
                section.AddToggle(
                    "use_animated_intent_icon",
                    ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-USE_ANIMATED_INTENT_ICON.title", "Use Animated Intent Icon"),
                    useAnimatedIntentIcon);
                section.AddToggle(
                    "show_current_move",
                    ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-SHOW_CURRENT_MOVE.title", "Show Current Move"),
                    showCurrentMove);
                section.AddEnumChoice(
                    "intent_graph_position",
                    ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-INTENT_GRAPH_POSITION.title", "Intent Graph Position"),
                    intentGraphPosition,
                    optionLabelFactory: (position) => ModSettingsText.LocString("settings_ui", $"INTENTGRAPH2-INTENT_GRAPH_POSITION.{position}", position.ToString()),
                    presentation: ModSettingsChoicePresentation.Stepper);
                section.AddSlider(
                    "intent_graph_scale",
                    ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-INTENT_GRAPH_SCALE.title", "Intent Graph Scale"),
                    intentGraphScale,
                    minValue: 50,
                    maxValue: 150,
                    step: 10,
                    valueFormatter: (value) => $"{value:0.##}%");
            });
            page.AddSection("control", section =>
            {
                section.WithTitle(ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-CONTROL.title", "Control"));
                section.AddToggle(
                    "pinable_intent_graph",
                    ModSettingsText.LocString("settings_ui", "INTENTGRAPH2-PINABLE_INTENT_GRAPH.title", "Pinable Intent Graph"),
                    pinableIntentGraph);
            });
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

    public void SaveConfig()
    {
        RitsuLibFramework.GetDataStore(IntentGraphMod.ModId).Save(SettingKey);
    }
}
