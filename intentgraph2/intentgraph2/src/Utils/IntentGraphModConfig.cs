using Godot;
using System;

namespace IntentGraph2.Utils;

public class IntentGraphModConfig
{
    public Key ToggleIntentGraphKey { get; set; } = Key.F1;

    public bool ShowMonsterMoveNames { get; set; } = true;

    public bool UseAnimatedIntentIcon { get; set; } = true;

    public bool ShowCurrentMove { get; set; } = true;

    public event EventHandler<string>? OnUpdated;

    public void NotifyUpdated(string propertyName)
    {
        OnUpdated?.Invoke(this, propertyName);
    }

    public void SetFrom(IntentGraphModConfig config)
    {
        ToggleIntentGraphKey = config.ToggleIntentGraphKey;
        ShowMonsterMoveNames = config.ShowMonsterMoveNames;
        UseAnimatedIntentIcon = config.UseAnimatedIntentIcon;
        ShowCurrentMove = config.ShowCurrentMove;
    }
}
