using Godot;
using IntentGraph2.Models;
using IntentGraph2.Utils;
using IntentGraph2.Utils.Rule;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntentGraph2.Scenes;

public partial class NIntentGraphEditor : Control
{
    private enum JsonEditorKind
    {
        SecondaryInitialStates,
        StateMachine,
        MoveReplacements,
        GraphPatch,
    }

    private enum PendingAction
    {
        None,
        Close,
        Reload,
    }

    private Godot.Label? monsterNameLabel;
    private Godot.Label? monsterModelLabel;
    private Godot.Label? headerStatusLabel;
    private ItemList? variantList;
    private ItemList? stateIdList;
    private Button? addVariantButton;
    private Button? duplicateVariantButton;
    private Button? deleteVariantButton;
    private Button? moveVariantUpButton;
    private Button? moveVariantDownButton;
    private Button? saveButton;
    private Button? reloadButton;
    private Button? closeButton;
    private LineEdit? conditionEdit;
    private Godot.Label? conditionStatusLabel;
    private CodeEdit? secondaryInitialStatesEdit;
    private CodeEdit? stateMachineEdit;
    private CodeEdit? moveReplacementsEdit;
    private CodeEdit? graphPatchEdit;
    private Godot.Label? intentStringsStatusLabel;
    private CodeEdit? intentStringsEdit;
    private CodeEdit? readOnlySummaryEdit;
    private Godot.Label? previewStatusLabel;
    private NIntentGraph? previewGraph;
    private ConfirmationDialog? unsavedChangesDialog;
    private AcceptDialog? messageDialog;
    private PanelContainer? windowPanel;
    private TabContainer? editorTabs;

    private MonsterModel? monster;
    private string monsterDisplayName = string.Empty;
    private string monsterModelFullName = string.Empty;
    private string loadedIntentStringsLanguage = "eng";
    private IntentDefinitionList draftDefinitions = new();
    private Dictionary<string, string> draftIntentStrings = new(StringComparer.Ordinal);
    private List<string> availableStateIds = new();
    private int selectedVariantIndex = -1;
    private bool isReady;
    private bool isRefreshingUi;
    private bool hasUnsavedChanges;
    private PendingAction pendingAction;
    private Control? lastActiveTextInput;

    public bool HasUnsavedChanges => hasUnsavedChanges;

    private static string LocalizeText(string key, string fallback)
    {
        return IntentGraphMod.IntentGraphStrings.GetValueOrDefault(key, fallback);
    }

    private static string LocalizeText(string key, string fallback, params object[] args)
    {
        return string.Format(LocalizeText(key, fallback), args);
    }

    public void Initialize(MonsterModel monster, string monsterDisplayName)
    {
        this.monster = monster;
        this.monsterDisplayName = monsterDisplayName;
        this.monsterModelFullName = monster.GetType().FullName ?? monster.Id.ToString();

        if (isReady)
        {
            LoadDefinitionsFromDiskOrRuntime();
        }
    }

    public override void _Ready()
    {
        monsterNameLabel = GetNode<Godot.Label>("%MonsterName");
        monsterModelLabel = GetNode<Godot.Label>("%MonsterModelName");
        headerStatusLabel = GetNode<Godot.Label>("%HeaderStatus");
        variantList = GetNode<ItemList>("%VariantList");
        stateIdList = GetNode<ItemList>("%StateIdList");
        addVariantButton = GetNode<Button>("%AddVariantButton");
        duplicateVariantButton = GetNode<Button>("%DuplicateVariantButton");
        deleteVariantButton = GetNode<Button>("%DeleteVariantButton");
        moveVariantUpButton = GetNode<Button>("%MoveVariantUpButton");
        moveVariantDownButton = GetNode<Button>("%MoveVariantDownButton");
        saveButton = GetNode<Button>("%SaveButton");
        reloadButton = GetNode<Button>("%ReloadButton");
        closeButton = GetNode<Button>("%CloseButton");
        conditionEdit = GetNode<LineEdit>("%ConditionEdit");
        conditionStatusLabel = GetNode<Godot.Label>("%ConditionStatus");
        secondaryInitialStatesEdit = GetNode<CodeEdit>("%SecondaryInitialStatesEdit");
        stateMachineEdit = GetNode<CodeEdit>("%StateMachineEdit");
        moveReplacementsEdit = GetNode<CodeEdit>("%MoveReplacementsEdit");
        graphPatchEdit = GetNode<CodeEdit>("%GraphPatchEdit");
        intentStringsStatusLabel = GetNode<Godot.Label>("%IntentStringsStatus");
        intentStringsEdit = GetNode<CodeEdit>("%IntentStringsEdit");
        readOnlySummaryEdit = GetNode<CodeEdit>("%ReadOnlySummaryEdit");
        previewStatusLabel = GetNode<Godot.Label>("%PreviewStatus");
        previewGraph = GetNode<NIntentGraph>("%PreviewGraph");
        unsavedChangesDialog = GetNode<ConfirmationDialog>("%UnsavedChangesDialog");
        messageDialog = GetNode<AcceptDialog>("%MessageDialog");
        windowPanel = GetNode<PanelContainer>("WindowMargin/Window");
        editorTabs = GetNode<TabContainer>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/ContentSplit/EditorScroll/EditorVBox/EditorTabs");

        ApplyLocalization();

        variantList.ItemSelected += OnVariantSelected;
        stateIdList.ItemClicked += OnStateIdClicked;
        addVariantButton.Pressed += OnAddVariantPressed;
        duplicateVariantButton.Pressed += OnDuplicateVariantPressed;
        deleteVariantButton.Pressed += OnDeleteVariantPressed;
        moveVariantUpButton.Pressed += OnMoveVariantUpPressed;
        moveVariantDownButton.Pressed += OnMoveVariantDownPressed;
        saveButton.Pressed += OnSavePressed;
        reloadButton.Pressed += OnReloadPressed;
        closeButton.Pressed += OnClosePressed;
        conditionEdit.TextChanged += _ => OnEditorFieldsChanged();
        secondaryInitialStatesEdit.TextChanged += OnEditorFieldsChanged;
        stateMachineEdit.TextChanged += OnEditorFieldsChanged;
        moveReplacementsEdit.TextChanged += OnEditorFieldsChanged;
        graphPatchEdit.TextChanged += OnEditorFieldsChanged;
        intentStringsEdit.TextChanged += OnIntentStringsChanged;
        secondaryInitialStatesEdit.TextChanged += () => OnCodeEditorTextChanged(secondaryInitialStatesEdit);
        stateMachineEdit.TextChanged += () => OnCodeEditorTextChanged(stateMachineEdit);
        moveReplacementsEdit.TextChanged += () => OnCodeEditorTextChanged(moveReplacementsEdit);
        graphPatchEdit.TextChanged += () => OnCodeEditorTextChanged(graphPatchEdit);
        secondaryInitialStatesEdit.CodeCompletionRequested += () => OnCodeCompletionRequested(secondaryInitialStatesEdit, JsonEditorKind.SecondaryInitialStates);
        stateMachineEdit.CodeCompletionRequested += () => OnCodeCompletionRequested(stateMachineEdit, JsonEditorKind.StateMachine);
        moveReplacementsEdit.CodeCompletionRequested += () => OnCodeCompletionRequested(moveReplacementsEdit, JsonEditorKind.MoveReplacements);
        graphPatchEdit.CodeCompletionRequested += () => OnCodeCompletionRequested(graphPatchEdit, JsonEditorKind.GraphPatch);
        unsavedChangesDialog.Confirmed += OnUnsavedChangesConfirmed;
        RegisterEditableInput(conditionEdit);
        RegisterEditableInput(secondaryInitialStatesEdit);
        RegisterEditableInput(stateMachineEdit);
        RegisterEditableInput(moveReplacementsEdit);
        RegisterEditableInput(graphPatchEdit);
        RegisterEditableInput(intentStringsEdit);
        ConfigureCodeEditor(secondaryInitialStatesEdit);
        ConfigureCodeEditor(stateMachineEdit);
        ConfigureCodeEditor(moveReplacementsEdit);
        ConfigureCodeEditor(graphPatchEdit);
        ConfigureCodeEditor(intentStringsEdit, enableCompletion: false);
        ConfigureCodeEditor(readOnlySummaryEdit, enableCompletion: false);

        readOnlySummaryEdit.Editable = false;
        isReady = true;
        CallDeferred(MethodName.ApplyViewportLayout);

        if (monster != null)
        {
            LoadDefinitionsFromDiskOrRuntime();
        }
        else
        {
            UpdateHeaderStatus(LocalizeText("ui.editor.status.no_monster", "No monster selected."));
            SetEditorsEnabled(false);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
        {
            RequestClose();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ApplyViewportLayout()
    {
        var viewportSize = GetViewportRect().Size;
        Position = Vector2.Zero;
        Size = viewportSize;

        if (windowPanel != null)
        {
            windowPanel.CustomMinimumSize = viewportSize;
            windowPanel.Size = viewportSize;
        }
    }

    private void LoadDefinitionsFromDiskOrRuntime()
    {
        availableStateIds = monster?.MoveStateMachine?.States.Values
            .Select(state => state.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList()
            ?? new List<string>();

        draftDefinitions = monster == null
            ? new IntentDefinitionList()
            : IntentDefinitionEditorService.LoadEditableDefinitions(monsterModelFullName);
        loadedIntentStringsLanguage = LocManager.Instance.Language;
        draftIntentStrings = IntentGraphMod.LoadIntentStringsFromFile(IntentGraphMod.GetDevIntentStringFilePath(loadedIntentStringsLanguage));
        ApplyLocalization();

        selectedVariantIndex = draftDefinitions.Count > 0 ? Math.Clamp(selectedVariantIndex, 0, draftDefinitions.Count - 1) : -1;
        if (draftDefinitions.Count > 0 && selectedVariantIndex < 0)
        {
            selectedVariantIndex = 0;
        }

        hasUnsavedChanges = false;
        pendingAction = PendingAction.None;
        RefreshUiFromState();
        UpdateHeaderStatus(LocalizeText("ui.editor.status.editing_monster", "Editing {0}", monsterModelFullName));
    }

    private void ApplyLocalization()
    {
        monsterNameLabel?.SetDeferred("text", LocalizeText("ui.editor.header.monster_name", "Monster Name"));
        monsterModelLabel?.SetDeferred("text", LocalizeText("ui.editor.header.monster_model_name", "Monster model full name"));
        if (string.IsNullOrWhiteSpace(headerStatusLabel?.Text) || headerStatusLabel?.Text == "Ready")
        {
            UpdateHeaderStatus(LocalizeText("ui.editor.status.ready", "Ready"));
        }

        saveButton?.SetDeferred("text", LocalizeText("ui.editor.button.save", "Save"));
        reloadButton?.SetDeferred("text", LocalizeText("ui.editor.button.reload", "Reload"));
        closeButton?.SetDeferred("text", LocalizeText("ui.editor.button.close", "Close"));
        addVariantButton?.SetDeferred("text", LocalizeText("ui.editor.button.add", "Add"));
        duplicateVariantButton?.SetDeferred("text", LocalizeText("ui.editor.button.duplicate", "Duplicate"));
        deleteVariantButton?.SetDeferred("text", LocalizeText("ui.editor.button.delete", "Delete"));
        moveVariantUpButton?.SetDeferred("text", LocalizeText("ui.editor.button.move_up", "Move Up"));
        moveVariantDownButton?.SetDeferred("text", LocalizeText("ui.editor.button.move_down", "Move Down"));

        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/Sidebar/VariantsLabel").Text = LocalizeText("ui.editor.sidebar.variants", "Variants");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/Sidebar/VariantHint").Text = LocalizeText("ui.editor.sidebar.variant_hint", "Later items win when multiple conditions match.");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/Sidebar/StateIdsLabel").Text = LocalizeText("ui.editor.sidebar.state_ids", "State IDs");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/Sidebar/StateIdHint").Text = LocalizeText("ui.editor.sidebar.state_ids_hint", "Click a state ID to insert it into the last focused editable field.");

        conditionEdit!.PlaceholderText = LocalizeText("ui.editor.condition.placeholder", "true");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/ContentSplit/EditorScroll/EditorVBox/EditorTabs/secondaryInitialStates JSON/SecondaryInitialStatesSection/SecondaryInitialStatesHelp").Text = LocalizeText("ui.editor.help.secondary_initial_states", "Edit secondaryInitialStates as a JSON array of state ids.");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/ContentSplit/EditorScroll/EditorVBox/EditorTabs/stateMachine JSON/StateMachineSection/StateMachineHelp").Text = LocalizeText("ui.editor.help.state_machine", "Edit the StateMachineNode array for this variant.");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/ContentSplit/EditorScroll/EditorVBox/EditorTabs/moveReplacements JSON/MoveReplacementsSection/MoveReplacementHelp").Text = LocalizeText("ui.editor.help.move_replacements", "Edit move replacement overrides keyed by move id.");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/ContentSplit/EditorScroll/EditorVBox/EditorTabs/graphPatch JSON/GraphPatchSection/GraphPatchHelp").Text = LocalizeText("ui.editor.help.graph_patch", "Edit the full graphPatch object for this variant.");
        GetNode<Godot.Label>("WindowMargin/Window/RootMargin/RootVBox/BodySplit/ContentSplit/PreviewVBox/PreviewTitle").Text = LocalizeText("ui.editor.preview.title", "Live Preview");

        if (editorTabs != null)
        {
            SetTabTitle("Condition", LocalizeText("ui.editor.tab.condition", "Condition"));
            SetTabTitle("secondaryInitialStates JSON", LocalizeText("ui.editor.tab.secondary_initial_states", "secondaryInitialStates JSON"));
            SetTabTitle("stateMachine JSON", LocalizeText("ui.editor.tab.state_machine", "stateMachine JSON"));
            SetTabTitle("moveReplacements JSON", LocalizeText("ui.editor.tab.move_replacements", "moveReplacements JSON"));
            SetTabTitle("graphPatch JSON", LocalizeText("ui.editor.tab.graph_patch", "graphPatch JSON"));
            SetTabTitle("intentgraph strings JSON", LocalizeText("ui.editor.tab.intent_strings", "intentgraph strings JSON"));
            SetTabTitle("Read-only fields", LocalizeText("ui.editor.tab.read_only", "Read-only fields"));
        }

        unsavedChangesDialog!.Title = LocalizeText("ui.editor.dialog.unsaved.title", "Discard Changes?");
        unsavedChangesDialog.DialogText = LocalizeText("ui.editor.dialog.unsaved.text", "You have unsaved changes. Continue and discard them?");
        messageDialog!.Title = LocalizeText("ui.editor.dialog.message.title", "Intent Graph Editor");

        if (conditionStatusLabel != null && conditionStatusLabel.Text == "Condition syntax is validated against the current monster.")
        {
            conditionStatusLabel.Text = LocalizeText("ui.editor.condition.status.initial", "Condition syntax is validated against the current monster.");
        }

        if (intentStringsStatusLabel != null && intentStringsStatusLabel.Text == "Editing dev intent strings for the current language.")
        {
            intentStringsStatusLabel.Text = LocalizeText("ui.editor.intent_strings.status.initial", "Editing dev intent strings for the current language.");
        }

        if (previewStatusLabel != null && previewStatusLabel.Text == "Preview updates as fields become valid.")
        {
            previewStatusLabel.Text = LocalizeText("ui.editor.preview.status.initial", "Preview updates as fields become valid.");
        }
    }

    private void SetTabTitle(string childName, string title)
    {
        if (editorTabs == null || !editorTabs.HasNode(childName))
        {
            return;
        }

        var tabControl = editorTabs.GetNode<Control>(childName);
        editorTabs.SetTabTitle(tabControl.GetIndex(), title);
    }

    private void RefreshUiFromState()
    {
        if (!isReady)
        {
            return;
        }

        isRefreshingUi = true;
        try
        {
            monsterNameLabel?.SetDeferred("text", monsterDisplayName);
            monsterModelLabel?.SetDeferred("text", monsterModelFullName);

            RefreshVariantList();
            RefreshStateIdList();
            LoadSelectedDefinitionIntoEditors();
            LoadIntentStringsIntoEditor();
            UpdateActionState();
        }
        finally
        {
            isRefreshingUi = false;
        }

        RefreshPreview();
    }

    private void RefreshVariantList()
    {
        if (variantList == null)
        {
            return;
        }

        variantList.Clear();
        for (int i = 0; i < draftDefinitions.Count; i++)
        {
            variantList.AddItem(BuildVariantLabel(i, draftDefinitions[i]));
        }

        if (selectedVariantIndex >= 0 && selectedVariantIndex < draftDefinitions.Count)
        {
            variantList.Select(selectedVariantIndex);
        }
    }

    private void RefreshStateIdList()
    {
        if (stateIdList == null)
        {
            return;
        }

        stateIdList.Clear();
        foreach (var stateId in availableStateIds)
        {
            stateIdList.AddItem(stateId);
        }
    }

    private static string BuildVariantLabel(int index, IntentDefinition definition)
    {
        var condition = string.IsNullOrWhiteSpace(definition.Condition) ? "true" : definition.Condition.Trim();
        if (condition.Length > 42)
        {
            condition = condition[..39] + "...";
        }

        return $"#{index + 1} {condition}";
    }

    private void LoadSelectedDefinitionIntoEditors()
    {
        var definition = selectedVariantIndex >= 0 && selectedVariantIndex < draftDefinitions.Count
            ? draftDefinitions[selectedVariantIndex]
            : null;
        SetEditorsEnabled(definition != null);

        if (conditionEdit == null || secondaryInitialStatesEdit == null || stateMachineEdit == null || moveReplacementsEdit == null || graphPatchEdit == null || readOnlySummaryEdit == null)
        {
            return;
        }

        if (definition == null)
        {
            conditionEdit.Text = string.Empty;
            secondaryInitialStatesEdit.Text = string.Empty;
            stateMachineEdit.Text = string.Empty;
            moveReplacementsEdit.Text = string.Empty;
            graphPatchEdit.Text = string.Empty;
            readOnlySummaryEdit.Text = LocalizeText("ui.editor.message.no_variant_selected", "No variant selected. Add one to begin editing.");
            UpdateConditionStatus(null);
            return;
        }

        conditionEdit.Text = definition.Condition ?? "true";
        secondaryInitialStatesEdit.Text = definition.SecondaryInitialStates == null || definition.SecondaryInitialStates.Length == 0
            ? string.Empty
            : IntentDefinitionEditorService.SerializeJson(definition.SecondaryInitialStates);
        stateMachineEdit.Text = definition.StateMachine == null
            ? string.Empty
            : IntentDefinitionEditorService.SerializeJson(definition.StateMachine);
        moveReplacementsEdit.Text = definition.MoveReplacements == null
            ? string.Empty
            : IntentDefinitionEditorService.SerializeJson(definition.MoveReplacements);
        graphPatchEdit.Text = definition.GraphPatch == null
            ? string.Empty
            : IntentDefinitionEditorService.SerializeJson(definition.GraphPatch);
        readOnlySummaryEdit.Text = IntentDefinitionEditorService.BuildReadOnlySummary(definition);
        UpdateConditionStatus(definition.Condition);
    }

    private void LoadIntentStringsIntoEditor()
    {
        if (intentStringsEdit == null)
        {
            return;
        }

        intentStringsEdit.Text = draftIntentStrings.Count == 0
            ? string.Empty
            : IntentDefinitionEditorService.SerializeJson(draftIntentStrings);
        UpdateIntentStringsStatus(null);
    }

    private void UpdateActionState()
    {
        var hasSelection = selectedVariantIndex >= 0 && selectedVariantIndex < draftDefinitions.Count;

        if (duplicateVariantButton != null)
        {
            duplicateVariantButton.Disabled = !hasSelection;
        }

        if (deleteVariantButton != null)
        {
            deleteVariantButton.Disabled = !hasSelection;
        }

        if (moveVariantUpButton != null)
        {
            moveVariantUpButton.Disabled = !hasSelection || selectedVariantIndex <= 0;
        }

        if (moveVariantDownButton != null)
        {
            moveVariantDownButton.Disabled = !hasSelection || selectedVariantIndex >= draftDefinitions.Count - 1;
        }
    }

    private void SetEditorsEnabled(bool enabled)
    {
        if (conditionEdit != null)
        {
            conditionEdit.Editable = enabled;
        }

        if (secondaryInitialStatesEdit != null)
        {
            secondaryInitialStatesEdit.Editable = enabled;
        }

        if (stateMachineEdit != null)
        {
            stateMachineEdit.Editable = enabled;
        }

        if (moveReplacementsEdit != null)
        {
            moveReplacementsEdit.Editable = enabled;
        }

        if (graphPatchEdit != null)
        {
            graphPatchEdit.Editable = enabled;
        }
    }

    private void OnEditorFieldsChanged()
    {
        if (isRefreshingUi)
        {
            return;
        }

        hasUnsavedChanges = true;
        UpdateHeaderStatus(LocalizeText("ui.editor.status.unsaved_changes", "Unsaved changes"));
        RefreshPreview();
    }

    private void OnIntentStringsChanged()
    {
        OnEditorFieldsChanged();

        if (TryReadIntentStringsFromEditor(out _, out var error))
        {
            UpdateIntentStringsStatus(null);
        }
        else
        {
            UpdateIntentStringsStatus(error);
        }
    }

    private void ConfigureCodeEditor(CodeEdit? editor, bool enableCompletion = true)
    {
        if (editor == null)
        {
            return;
        }

        editor.CodeCompletionEnabled = enableCompletion;
        editor.AutoBraceCompletionEnabled = true;
        editor.IndentAutomatic = true;
        editor.IndentUseSpaces = true;
        editor.IndentSize = 4;
        editor.GuttersDrawLineNumbers = true;
    }

    private void OnCodeEditorTextChanged(CodeEdit? editor)
    {
        if (editor == null || isRefreshingUi || !editor.HasFocus())
        {
            return;
        }

        var line = editor.GetLine(editor.GetCaretLine());
        var column = editor.GetCaretColumn();
        var previousChar = column > 0 && column <= line.Length ? line[column - 1] : '\0';
        var prefix = GetCompletionPrefix(editor);
        if (previousChar == '"' || previousChar == ':' || previousChar == '{' || previousChar == '[' || prefix.Length >= 1)
        {
            editor.RequestCodeCompletion(force: true);
        }
    }

    private void OnCodeCompletionRequested(CodeEdit? editor, JsonEditorKind kind)
    {
        if (editor == null)
        {
            return;
        }

        var isInsideString = IsCaretInsideString(editor);
        var prefix = GetCompletionPrefix(editor);
        foreach (var completion in BuildCompletionItems(kind, isInsideString)
                     .Where(c => string.IsNullOrEmpty(prefix) || c.MatchText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                     .GroupBy(c => (c.DisplayText, c.InsertText))
                     .Select(group => group.First()))
        {
            editor.AddCodeCompletionOption(completion.KindValue, completion.DisplayText, completion.InsertText);
        }

        editor.UpdateCodeCompletionOptions(force: true);
    }

    private void RegisterEditableInput(Control? control)
    {
        if (control == null)
        {
            return;
        }

        control.FocusEntered += () =>
        {
            lastActiveTextInput = control;
        };
    }

    private void OnVariantSelected(long index)
    {
        if (isRefreshingUi)
        {
            return;
        }

        var nextIndex = (int)index;
        if (nextIndex == selectedVariantIndex)
        {
            return;
        }

        if (!TryCommitSelectedVariant(validateCondition: false, out var error))
        {
            ShowMessage(LocalizeText("ui.editor.message.fix_before_switch", "Fix the current variant before switching.\n\n{0}", error));
            isRefreshingUi = true;
            try
            {
                if (variantList != null && selectedVariantIndex >= 0 && selectedVariantIndex < draftDefinitions.Count)
                {
                    variantList.Select(selectedVariantIndex);
                }
            }
            finally
            {
                isRefreshingUi = false;
            }
            return;
        }

        selectedVariantIndex = nextIndex;
        isRefreshingUi = true;
        try
        {
            LoadSelectedDefinitionIntoEditors();
            UpdateActionState();
        }
        finally
        {
            isRefreshingUi = false;
        }

        RefreshPreview();
    }

    private void OnAddVariantPressed()
    {
        if (!TryCommitSelectedVariant(validateCondition: false, out var error))
        {
            ShowMessage(LocalizeText("ui.editor.message.fix_before_add", "Fix the current variant before adding another.\n\n{0}", error));
            return;
        }

        draftDefinitions.Add(new IntentDefinition());
        selectedVariantIndex = draftDefinitions.Count - 1;
        hasUnsavedChanges = true;
        RefreshUiFromState();
    }

    private void OnStateIdClicked(long index, Vector2 atPosition, long mouseButtonIndex)
    {
        if (mouseButtonIndex != (long)MouseButton.Left)
        {
            return;
        }

        if (index < 0 || index >= availableStateIds.Count)
        {
            return;
        }

        InsertStateIdIntoActiveInput(availableStateIds[(int)index]);
    }

    private void OnDuplicateVariantPressed()
    {
        if (selectedVariantIndex < 0 || selectedVariantIndex >= draftDefinitions.Count)
        {
            return;
        }

        if (!TryCommitSelectedVariant(validateCondition: false, out var error))
        {
            ShowMessage(LocalizeText("ui.editor.message.fix_before_duplicate", "Fix the current variant before duplicating it.\n\n{0}", error));
            return;
        }

        var clone = IntentDefinitionEditorService.Clone(draftDefinitions[selectedVariantIndex]) ?? new IntentDefinition();
        draftDefinitions.Insert(selectedVariantIndex + 1, clone);
        selectedVariantIndex++;
        hasUnsavedChanges = true;
        RefreshUiFromState();
    }

    private void OnDeleteVariantPressed()
    {
        if (selectedVariantIndex < 0 || selectedVariantIndex >= draftDefinitions.Count)
        {
            return;
        }

        draftDefinitions.RemoveAt(selectedVariantIndex);
        if (draftDefinitions.Count == 0)
        {
            selectedVariantIndex = -1;
        }
        else if (selectedVariantIndex >= draftDefinitions.Count)
        {
            selectedVariantIndex = draftDefinitions.Count - 1;
        }

        hasUnsavedChanges = true;
        RefreshUiFromState();
    }

    private void OnMoveVariantUpPressed()
    {
        MoveSelectedVariant(-1);
    }

    private void OnMoveVariantDownPressed()
    {
        MoveSelectedVariant(1);
    }

    private void MoveSelectedVariant(int delta)
    {
        if (selectedVariantIndex < 0 || selectedVariantIndex >= draftDefinitions.Count)
        {
            return;
        }

        if (!TryCommitSelectedVariant(validateCondition: false, out var error))
        {
            ShowMessage(LocalizeText("ui.editor.message.fix_before_reorder", "Fix the current variant before reordering.\n\n{0}", error));
            return;
        }

        var nextIndex = selectedVariantIndex + delta;
        if (nextIndex < 0 || nextIndex >= draftDefinitions.Count)
        {
            return;
        }

        (draftDefinitions[selectedVariantIndex], draftDefinitions[nextIndex]) = (draftDefinitions[nextIndex], draftDefinitions[selectedVariantIndex]);
        selectedVariantIndex = nextIndex;
        hasUnsavedChanges = true;
        RefreshUiFromState();
    }

    private void OnSavePressed()
    {
        if (!TryCommitSelectedVariant(validateCondition: true, out var error))
        {
            ShowMessage(LocalizeText("ui.editor.message.save_invalid_current", "Cannot save until the current variant is valid.\n\n{0}", error));
            return;
        }

        if (!ValidateAllVariantConditions(out error))
        {
            ShowMessage(LocalizeText("ui.editor.message.save_invalid_all_conditions", "Cannot save until every variant has a valid condition.\n\n{0}", error));
            return;
        }

        if (!TryCommitIntentStrings(out error))
        {
            ShowMessage(LocalizeText("ui.editor.message.save_invalid_intent_strings", "Cannot save until the current language intent strings JSON is valid.\n\n{0}", error));
            return;
        }

        IntentDefinitionEditorService.SaveEditableDefinitions(monsterModelFullName, draftDefinitions);
        IntentGraphMod.SaveIntentStringsToFile(IntentGraphMod.GetDevIntentStringFilePath(loadedIntentStringsLanguage), draftIntentStrings);
        IntentGraphMod.ReloadIntentDefinitionsAndGraphs();
        hasUnsavedChanges = false;
        UpdateHeaderStatus(LocalizeText("ui.editor.status.saved", "Saved definitions and {0} intent strings.", loadedIntentStringsLanguage));
        RefreshUiFromState();
    }

    private void OnReloadPressed()
    {
        if (hasUnsavedChanges)
        {
            pendingAction = PendingAction.Reload;
            unsavedChangesDialog?.PopupCentered();
            return;
        }

        ReloadDefinitions();
    }

    private void OnClosePressed()
    {
        RequestClose();
    }

    private void RequestClose()
    {
        if (hasUnsavedChanges)
        {
            pendingAction = PendingAction.Close;
            unsavedChangesDialog?.PopupCentered();
            return;
        }

        CloseEditor();
    }

    private void OnUnsavedChangesConfirmed()
    {
        switch (pendingAction)
        {
            case PendingAction.Close:
                CloseEditor();
                break;
            case PendingAction.Reload:
                ReloadDefinitions();
                break;
        }

        pendingAction = PendingAction.None;
    }

    private void ReloadDefinitions()
    {
        LoadDefinitionsFromDiskOrRuntime();
    }

    private void CloseEditor()
    {
        IntentGraphEditorHost.CloseEditor(this);
    }

    private void RefreshPreview()
    {
        if (previewGraph == null || previewStatusLabel == null)
        {
            return;
        }

        if (monster == null)
        {
            previewGraph.Graph = null;
            previewStatusLabel.Text = LocalizeText("ui.editor.preview.no_monster", "No monster selected.");
            return;
        }

        if (!TryReadIntentStringsFromEditor(out var previewIntentStrings, out var stringError))
        {
            previewGraph.Graph = null;
            previewStatusLabel.Text = LocalizeText("ui.editor.preview.blocked", "Preview blocked: {0}", stringError);
            return;
        }

        if (selectedVariantIndex < 0 || selectedVariantIndex >= draftDefinitions.Count)
        {
            previewGraph.Graph = IntentGraphGenerator.GenerateGraph(monster, overwriteIntentDefinition: null, previewIntentStrings);
            previewStatusLabel.Text = previewGraph.Graph == null
                ? LocalizeText("ui.editor.preview.no_graph_current_monster", "Preview generated no graph for the current monster.")
                : LocalizeText("ui.editor.preview.updated_runtime", "Preview updated from current runtime graph.");
            UpdateConditionStatus(null);
            return;
        }

        if (!TryCommitSelectedVariant(validateCondition: false, out var error))
        {
            previewStatusLabel.Text = LocalizeText("ui.editor.preview.blocked", "Preview blocked: {0}", error);
            return;
        }

        var definition = draftDefinitions[selectedVariantIndex];
        UpdateConditionStatus(definition.Condition);

        try
        {
            previewGraph.Graph = IntentGraphGenerator.GenerateGraph(monster, definition, previewIntentStrings);
            previewStatusLabel.Text = previewGraph.Graph == null
                ? LocalizeText("ui.editor.preview.no_graph_draft", "Preview generated no graph for this draft.")
                : LocalizeText("ui.editor.preview.updated", "Preview updated.");
        }
        catch (Exception ex)
        {
            previewGraph.Graph = null;
            previewStatusLabel.Text = LocalizeText("ui.editor.preview.error", "Preview error: {0}", ex.Message);
        }
    }

    private bool TryCommitSelectedVariant(bool validateCondition, out string error)
    {
        error = string.Empty;
        if (selectedVariantIndex < 0 || selectedVariantIndex >= draftDefinitions.Count)
        {
            return true;
        }

        var source = IntentDefinitionEditorService.Clone(draftDefinitions[selectedVariantIndex]) ?? new IntentDefinition();
        source.Condition = string.IsNullOrWhiteSpace(conditionEdit?.Text) ? "true" : conditionEdit!.Text.Trim();
        source.ParsedCondition = null;

        if (!TryDeserializeField(secondaryInitialStatesEdit?.Text, LocalizeText("ui.editor.field.secondary_initial_states", "secondaryInitialStates"), out string[]? secondaryInitialStates, out error))
        {
            return false;
        }

        if (!TryDeserializeField(stateMachineEdit?.Text, LocalizeText("ui.editor.field.state_machine", "stateMachine"), out StateMachineNode[]? stateMachine, out error))
        {
            return false;
        }

        if (!TryDeserializeField(moveReplacementsEdit?.Text, LocalizeText("ui.editor.field.move_replacements", "moveReplacements"), out Dictionary<string, MoveReplacement[]>? moveReplacements, out error))
        {
            return false;
        }

        if (!TryDeserializeField(graphPatchEdit?.Text, LocalizeText("ui.editor.field.graph_patch", "graphPatch"), out Graph? graphPatch, out error))
        {
            return false;
        }

        source.SecondaryInitialStates = NormalizeSecondaryInitialStates(secondaryInitialStates);
        source.StateMachine = stateMachine;
        source.MoveReplacements = moveReplacements;
        source.GraphPatch = NormalizeGraphPatch(graphPatch);
        if (validateCondition && !ValidateCondition(source.Condition, out error))
        {
            return false;
        }

        draftDefinitions[selectedVariantIndex] = source;
        return true;
    }

    private bool TryCommitIntentStrings(out string error)
    {
        if (!TryReadIntentStringsFromEditor(out var intentStrings, out error))
        {
            return false;
        }

        draftIntentStrings = intentStrings;
        return true;
    }

    private bool ValidateAllVariantConditions(out string error)
    {
        for (int i = 0; i < draftDefinitions.Count; i++)
        {
            var condition = string.IsNullOrWhiteSpace(draftDefinitions[i].Condition) ? "true" : draftDefinitions[i].Condition.Trim();
            if (!ValidateCondition(condition, out error))
            {
                error = $"Variant #{i + 1}: {error}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateCondition(string condition, out string error)
    {
        error = string.Empty;
        if (monster == null)
        {
            return true;
        }

        try
        {
            if (IRule.Parse(condition, new RuleContext(monster)) == null)
            {
                error = LocalizeText("ui.editor.condition.parse_error", "Condition could not be parsed.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void UpdateConditionStatus(string? condition)
    {
        if (conditionStatusLabel == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(condition))
        {
            conditionStatusLabel.Text = LocalizeText("ui.editor.condition.defaults_true", "Condition defaults to true.");
            conditionStatusLabel.Modulate = Colors.White;
            return;
        }

        if (ValidateCondition(condition, out var error))
        {
            conditionStatusLabel.Text = LocalizeText("ui.editor.condition.valid", "Condition syntax is valid.");
            conditionStatusLabel.Modulate = new Color(0.7f, 0.95f, 0.75f);
        }
        else
        {
            conditionStatusLabel.Text = LocalizeText("ui.editor.condition.warning", "Condition warning: {0}", error);
            conditionStatusLabel.Modulate = new Color(1.0f, 0.7f, 0.65f);
        }
    }

    private void UpdateHeaderStatus(string status)
    {
        if (headerStatusLabel != null)
        {
            headerStatusLabel.Text = status;
        }
    }

    private void UpdateIntentStringsStatus(string? error)
    {
        if (intentStringsStatusLabel == null)
        {
            return;
        }

        var filePath = IntentGraphMod.GetDevIntentStringFilePath(loadedIntentStringsLanguage);
        if (string.IsNullOrWhiteSpace(error))
        {
            intentStringsStatusLabel.Text = LocalizeText("ui.editor.intent_strings.status.saved_to", "Editing dev intent strings for '{0}'. Saved to {1}.", loadedIntentStringsLanguage, filePath);
            intentStringsStatusLabel.Modulate = Colors.White;
            return;
        }

        intentStringsStatusLabel.Text = LocalizeText("ui.editor.intent_strings.status.warning", "Intent strings warning: {0}", error);
        intentStringsStatusLabel.Modulate = new Color(1.0f, 0.7f, 0.65f);
    }

    private void InsertStateIdIntoActiveInput(string stateId)
    {
        switch (lastActiveTextInput)
        {
            case LineEdit lineEdit when lineEdit.Editable:
                lineEdit.DeleteText(lineEdit.GetSelectionFromColumn(), lineEdit.GetSelectionToColumn());
                lineEdit.InsertTextAtCaret(stateId);
                lineEdit.GrabFocus();
                break;
            case TextEdit textEdit when textEdit.Editable:
                textEdit.DeleteSelection();
                textEdit.InsertTextAtCaret(stateId);
                textEdit.GrabFocus();
                break;
            default:
                UpdateHeaderStatus(LocalizeText("ui.editor.status.click_editable_first", "Click in an editable text box first, then choose a state ID."));
                return;
        }

        UpdateHeaderStatus(LocalizeText("ui.editor.status.inserted_state_id", "Inserted state ID '{0}'.", stateId));
        OnEditorFieldsChanged();
    }

    private IEnumerable<CompletionItem> BuildCompletionItems(JsonEditorKind kind, bool isInsideString)
    {
        return kind switch
        {
            JsonEditorKind.SecondaryInitialStates => BuildSecondaryInitialStatesCompletionItems(isInsideString),
            JsonEditorKind.StateMachine => BuildStateMachineCompletionItems(isInsideString),
            JsonEditorKind.MoveReplacements => BuildMoveReplacementCompletionItems(isInsideString),
            JsonEditorKind.GraphPatch => BuildGraphPatchCompletionItems(isInsideString),
            _ => Enumerable.Empty<CompletionItem>(),
        };
    }

    private IEnumerable<CompletionItem> BuildSecondaryInitialStatesCompletionItems(bool isInsideString)
    {
        yield return SnippetCompletion("secondary initial states", "[\n  \"\"\n]");

        foreach (var stateId in availableStateIds)
        {
            yield return StringValueCompletion(stateId, isInsideString, CodeEdit.CodeCompletionKind.Variable);
        }
    }

    private IEnumerable<CompletionItem> BuildStateMachineCompletionItems(bool isInsideString)
    {
        foreach (var propertyName in new[] { "name", "moveName", "isInitialState", "initialStatePriority", "children", "followUpState", "label", "node" })
        {
            yield return PropertyCompletion(propertyName, isInsideString);
        }

        yield return SnippetCompletion("state machine node", "{\n  \"name\": \"\",\n  \"moveName\": \"\",\n  \"isInitialState\": false,\n  \"initialStatePriority\": 0,\n  \"followUpState\": \"\"\n}");
        yield return SnippetCompletion("state machine branch node", "{\n  \"name\": \"\",\n  \"children\": [\n    {\n      \"label\": \"\",\n      \"node\": {\n        \"name\": \"\"\n      }\n    }\n  ]\n}");
        yield return BooleanCompletion(true);
        yield return BooleanCompletion(false);

        foreach (var stateId in availableStateIds)
        {
            yield return StringValueCompletion(stateId, isInsideString, CodeEdit.CodeCompletionKind.Variable);
        }
    }

    private IEnumerable<CompletionItem> BuildMoveReplacementCompletionItems(bool isInsideString)
    {
        foreach (var stateId in availableStateIds)
        {
            yield return PropertyCompletion(stateId, isInsideString, CodeEdit.CodeCompletionKind.Variable);
        }

        yield return PropertyCompletion("valueText", isInsideString);
        yield return PropertyCompletion("timesText", isInsideString);
        yield return SnippetCompletion("move replacement entry", "[\n  {\n    \"valueText\": \"\",\n    \"timesText\": \"\"\n  }\n]");
        yield return PlainTextCompletion("null", "null");
    }

    private IEnumerable<CompletionItem> BuildGraphPatchCompletionItems(bool isInsideString)
    {
        foreach (var propertyName in new[] { "width", "height", "moves", "icons", "iconGroups", "labels", "arrows", "x", "y", "id", "intentType", "value", "times", "valueText", "timesText", "text", "align", "path" })
        {
            yield return PropertyCompletion(propertyName, isInsideString);
        }

        yield return SnippetCompletion("graphPatch", "{\n  \"labels\": [\n    {\n      \"x\": 0.0,\n      \"y\": 0.0,\n      \"text\": \"\",\n      \"align\": \"left\"\n    }\n  ]\n}");
        yield return SnippetCompletion("graphPatch label", "{\n  \"x\": 0.0,\n  \"y\": 0.0,\n  \"text\": \"\",\n  \"align\": \"left\"\n}");
        yield return SnippetCompletion("graphPatch move", "{\n  \"x\": 0.0,\n  \"y\": 0.0,\n  \"id\": \"\"\n}");
        yield return SnippetCompletion("graphPatch icon", "{\n  \"x\": 0.0,\n  \"y\": 0.0,\n  \"intentType\": \"Attack\",\n  \"value\": 0,\n  \"times\": 1\n}");
        yield return SnippetCompletion("graphPatch iconGroup", "{\n  \"x\": 0.0,\n  \"y\": 0.0,\n  \"width\": 1.0,\n  \"height\": 1.0\n}");
        yield return SnippetCompletion("graphPatch arrow", "{\n  \"path\": [0, 0.0, 0.0, 1.0]\n}");
        yield return StringValueCompletion("left", isInsideString, CodeEdit.CodeCompletionKind.Enum);
        yield return StringValueCompletion("center", isInsideString, CodeEdit.CodeCompletionKind.Enum);
        yield return StringValueCompletion("right", isInsideString, CodeEdit.CodeCompletionKind.Enum);

        foreach (var typeName in Enum.GetNames<IntentType>())
        {
            yield return StringValueCompletion(typeName, isInsideString, CodeEdit.CodeCompletionKind.Enum);
        }

        foreach (var stateId in availableStateIds)
        {
            yield return StringValueCompletion(stateId, isInsideString, CodeEdit.CodeCompletionKind.Variable);
        }
    }

    private static CompletionItem PropertyCompletion(string propertyName, bool isInsideString, CodeEdit.CodeCompletionKind kindValue = CodeEdit.CodeCompletionKind.Member)
    {
        return isInsideString
            ? new CompletionItem(propertyName, propertyName, propertyName, kindValue)
            : new CompletionItem(propertyName, $"\"{propertyName}\"", $"\"{propertyName}\": ", kindValue);
    }

    private static CompletionItem StringValueCompletion(string value, bool isInsideString, CodeEdit.CodeCompletionKind kindValue)
    {
        return isInsideString
            ? new CompletionItem(value, value, value, kindValue)
            : new CompletionItem(value, $"\"{value}\"", $"\"{value}\"", kindValue);
    }

    private static CompletionItem BooleanCompletion(bool value)
    {
        var text = value ? "true" : "false";
        return new CompletionItem(text, text, text, CodeEdit.CodeCompletionKind.Constant);
    }

    private static CompletionItem SnippetCompletion(string displayText, string insertText)
    {
        return new CompletionItem(displayText, displayText, insertText, CodeEdit.CodeCompletionKind.PlainText);
    }

    private static CompletionItem PlainTextCompletion(string displayText, string insertText)
    {
        return new CompletionItem(displayText, displayText, insertText, CodeEdit.CodeCompletionKind.PlainText);
    }

    private static string GetCompletionPrefix(CodeEdit editor)
    {
        var line = editor.GetLine(editor.GetCaretLine());
        var column = Math.Min(editor.GetCaretColumn(), line.Length);
        var start = column;
        while (start > 0 && IsCompletionCharacter(line[start - 1]))
        {
            start--;
        }

        return line.Substring(start, column - start);
    }

    private static bool IsCompletionCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character == '_' || character == '-';
    }

    private static bool IsCaretInsideString(CodeEdit editor)
    {
        var line = editor.GetCaretLine();
        var column = editor.GetCaretColumn();
        return editor.IsInString(line, column) >= 0 || (column > 0 && editor.IsInString(line, column - 1) >= 0);
    }

    private readonly record struct CompletionItem(string MatchText, string DisplayText, string InsertText, CodeEdit.CodeCompletionKind KindValue);

    private void ShowMessage(string text)
    {
        if (messageDialog == null)
        {
            return;
        }

        messageDialog.DialogText = text;
        messageDialog.PopupCentered();
    }

    private static Graph? NormalizeGraphPatch(Graph? graphPatch)
    {
        if (graphPatch == null)
        {
            return null;
        }

        if (graphPatch.Width == 1
            && graphPatch.Height == 1
            && graphPatch.Moves.Count == 0
            && graphPatch.Arrows.Count == 0
            && graphPatch.Icons.Count == 0
            && graphPatch.IconGroups.Count == 0
            && graphPatch.Labels.Count == 0)
        {
            return null;
        }

        return graphPatch;
    }

    private static string[]? NormalizeSecondaryInitialStates(string[]? secondaryInitialStates)
    {
        if (secondaryInitialStates == null)
        {
            return null;
        }

        var normalizedStateIds = secondaryInitialStates
            .Where(stateId => !string.IsNullOrWhiteSpace(stateId))
            .Select(stateId => stateId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalizedStateIds.Length == 0 ? null : normalizedStateIds;
    }

    private bool TryDeserializeField<T>(string? text, string fieldName, out T? value, out string error)
    {
        error = string.Empty;
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        try
        {
            value = IntentDefinitionEditorService.DeserializeJson<T>(text);
            return true;
        }
        catch (Exception ex)
        {
            error = LocalizeText("ui.editor.error.invalid_json", "Invalid {0} JSON: {1}", fieldName, ex.Message);
            return false;
        }
    }

    private bool TryReadIntentStringsFromEditor(out Dictionary<string, string> intentStrings, out string error)
    {
        intentStrings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(intentStringsEdit?.Text))
        {
            error = string.Empty;
            return true;
        }

        if (!TryDeserializeField(intentStringsEdit.Text, LocalizeText("ui.editor.field.intent_strings", "intent strings"), out Dictionary<string, string>? parsedStrings, out error))
        {
            return false;
        }

        intentStrings = parsedStrings ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return true;
    }
}