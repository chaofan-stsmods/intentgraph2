# Intent Graph Editor

## Purpose

This document summarizes the in-combat intent graph editor feature so future work can start from the current implementation instead of rediscovering the architecture.

The editor is used to author per-monster intent graph overrides during combat, preview changes live, and save them into dev override files in the game folder.

## Entry Point

- Console command: `editintent <monster model full name>`
- Opens the editor for the first matching live enemy in the current combat.
- Command implementation: `intentgraph2/intentgraph2/src/DevConsole/EditIntentConsoleCmd.cs`
- Editor host/lifecycle: `intentgraph2/intentgraph2/src/Utils/IntentGraphEditorHost.cs`

## Main Files

- Scene: `intentgraph2/intentgraph2/scenes/intent_graph_editor.tscn`
- Controller: `intentgraph2/intentgraph2/src/Scenes/NIntentGraphEditor.cs`
- Definition editor helper: `intentgraph2/intentgraph2/src/Utils/IntentDefinitionEditorService.cs`
- Mod-level load/save/reload helpers: `intentgraph2/intentgraph2/src/IntentGraphMod.cs`
- Preview generator: `intentgraph2/intentgraph2/src/Utils/IntentGraphGenerator.cs`

## Saved Files

- Intent definition overrides: `intentgraph-intents-dev.json`
- Intent string overrides for the current language: `intentgraph-strings-<language>-dev.json`

Both files are stored in the game folder. Paths are resolved through `IntentGraphMod` helpers.

## Current Editable Tabs

The editor currently exposes these tabs:

- `Condition`
- `secondaryInitialStates JSON`
- `stateMachine JSON`
- `moveReplacements JSON`
- `graphPatch JSON`
- `intentgraph strings JSON`
- `Read-only fields`

### Meaning of each tab

- `Condition`: rule expression used to select the variant.
- `secondaryInitialStates JSON`: editable `string[]` for additional graph roots.
- `stateMachine JSON`: editable `StateMachineNode[]` override.
- `moveReplacements JSON`: editable `Dictionary<string, MoveReplacement[]>`.
- `graphPatch JSON`: editable full `Graph` object used as `graphPatch`.
- `intentgraph strings JSON`: editable current-language `Dictionary<string, string>` dev override file.
- `Read-only fields`: currently only summarizes data that remains read-only.

## Read-only Scope

Only `graph` remains read-only in the editor summary.

Previously `graphPatch.labels` and `secondaryInitialStates` were partially or fully read-only, but that is no longer true.

## Variant Model

- Each monster model maps to an `IntentDefinitionList`.
- Variant order matters: the last matching condition wins.
- The editor supports add, duplicate, delete, and reorder operations on variants.

## Load Behavior

When the editor opens or reloads:

1. It collects available state IDs from the live monster's `MoveStateMachine`.
2. It loads the selected monster's editable definitions using `IntentDefinitionEditorService.LoadEditableDefinitions(...)`.
3. It loads dev intent strings for the current `LocManager.Instance.Language`.
4. It populates the active variant fields and live preview.

## Save Behavior

When saving:

1. The current variant is committed from the UI back into a draft `IntentDefinition`.
2. All variant conditions are validated.
3. Current-language dev intent strings JSON is validated.
4. Definitions are saved via `IntentDefinitionEditorService.SaveEditableDefinitions(...)`.
5. Intent strings are saved via `IntentGraphMod.SaveIntentStringsToFile(...)`.
6. Runtime data is refreshed via `IntentGraphMod.ReloadIntentDefinitionsAndGraphs()`.

## Preview Behavior

Preview is rendered with the existing `NIntentGraph` scene/control.

- If a variant is selected, preview uses the draft `IntentDefinition` plus draft string overrides.
- If no variant is selected, preview falls back to the monster's current runtime graph but still applies unsaved draft string overrides.
- Invalid JSON or invalid conditions block preview and show an inline status message.

Preview generation route:

- `NIntentGraphEditor.RefreshPreview()`
- `IntentGraphGenerator.GenerateGraph(monster, definitionOverride, stringOverrides)`

## Graph/String Override Notes

`IntentGraphGenerator` was extended so preview can accept temporary intent string overrides without mutating global runtime dictionaries.

This matters for the `intentgraph strings JSON` tab: label text and branch text can preview before the user saves.

## Completion Support

`NIntentGraphEditor` uses `CodeEdit` completion for structured JSON tabs.

Current completion groups:

- `secondaryInitialStates`: suggests state IDs
- `stateMachine`: state-machine field names and state IDs
- `moveReplacements`: replacement fields and state IDs
- `graphPatch`: graph patch fields, intent types, and state IDs

Completion items now store `CodeEdit.CodeCompletionKind` directly rather than `int`.

## Normalization Rules

- Empty or whitespace `Condition` becomes `true`.
- Empty `secondaryInitialStates` becomes `null`.
- `secondaryInitialStates` is trimmed, deduplicated, and nullified when empty.
- Empty `graphPatch` becomes `null` if it contains only default values and no content.

## Important Controller Methods

Useful starting points in `NIntentGraphEditor.cs`:

- `_Ready()`
- `LoadDefinitionsFromDiskOrRuntime()`
- `LoadSelectedDefinitionIntoEditors()`
- `RefreshPreview()`
- `TryCommitSelectedVariant(...)`
- `BuildCompletionItems(...)`
- `TryReadIntentStringsFromEditor(...)`

## Current Tests

Relevant tests are in `intentgraph2test/IntentDefinitionEditorServiceTest.cs`.

Covered areas currently include:

- saving merged definition overrides
- loading dev overrides before runtime definitions
- string dev file round-trip
- read-only summary content

## Known Caveat

Test builds may emit the existing Godot generator warning:

- `ScriptPathAttributeGenerator`: `GodotProjectDir` is null or empty

This warning has not been blocking compilation or test execution.

## Recommended Starting Points For Future Changes

- UI/layout changes: `intent_graph_editor.tscn`
- editor field behavior: `NIntentGraphEditor.cs`
- serialization rules: `IntentGraphMod.cs` and `IntentDefinitionEditorService.cs`
- preview/rendering behavior: `IntentGraphGenerator.cs`