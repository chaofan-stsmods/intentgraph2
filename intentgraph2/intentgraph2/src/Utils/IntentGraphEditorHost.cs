using Godot;
using IntentGraph2.Scenes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace IntentGraph2.Utils;

public static class IntentGraphEditorHost
{
    private const string EditorScenePath = "res://intentgraph2/scenes/intent_graph_editor.tscn";

    private static NIntentGraphEditor? currentEditor;

    public static bool TryOpenEditor(MonsterModel monster, string monsterDisplayName, out string message)
    {
        if (IsEditorAlive(currentEditor))
        {
            if (currentEditor!.HasUnsavedChanges)
            {
                message = "An intent editor is already open with unsaved changes. Close it before opening another editor.";
                return false;
            }

            currentEditor.QueueFree();
            currentEditor = null;
        }

        var parent = ResolveEditorParent();
        if (parent == null)
        {
            message = "Failed to find a UI parent for the intent editor.";
            return false;
        }

        var scene = ResourceLoader.Load<PackedScene>(EditorScenePath);
        if (scene == null)
        {
            message = $"Failed to load editor scene '{EditorScenePath}'.";
            return false;
        }

        var editor = scene.Instantiate<NIntentGraphEditor>(PackedScene.GenEditState.Disabled);
        parent.AddChild(editor);
        editor.TopLevel = true;
        editor.Position = Vector2.Zero;
        editor.Size = parent is Control parentControl
            ? parentControl.Size
            : editor.GetViewportRect().Size;
        editor.ZIndex = 1000;
        editor.Initialize(monster, monsterDisplayName);
        editor.TreeExited += () =>
        {
            if (ReferenceEquals(currentEditor, editor))
            {
                currentEditor = null;
            }
        };

        currentEditor = editor;
        message = $"Opened intent editor for {monsterDisplayName}.";
        return true;
    }

    public static void CloseEditor(NIntentGraphEditor editor)
    {
        if (ReferenceEquals(currentEditor, editor))
        {
            currentEditor = null;
        }

        if (GodotObject.IsInstanceValid(editor))
        {
            editor.QueueFree();
        }
    }

    private static Node? ResolveEditorParent()
    {
        if (NGame.Instance != null)
        {
            return NGame.Instance;
        }

        return (Engine.GetMainLoop() as SceneTree)?.Root;
    }

    private static bool IsEditorAlive(NIntentGraphEditor? editor)
    {
        return editor != null && GodotObject.IsInstanceValid(editor);
    }
}