using Godot;
using System;

namespace IntentGraph2.Utils;

public class IntentGraphModConfig
{
    public Key ToggleIntentGraphKey { get; set; } = Key.F1;

    public bool ShowMonsterMoveNames { get; set; } = true;

    public bool ShowMoveDetail { get; set; } = false;

    public bool UseAnimatedIntentIcon { get; set; } = true;

    public bool ShowCurrentMove { get; set; } = true;

    public bool PinableIntentGraph { get; set; } = false;

    public IntentGraphPosition IntentGraphPosition { get; set; } = IntentGraphPosition.Default;

    public float IntentGraphScale { get; set; } = 1.0f;

    public string AttackDamageFormat { get; set; } = "{base}";

    public IntentGraphHoverTrigger IntentGraphHoverTrigger { get; set; } = IntentGraphHoverTrigger.Monster;

    public event EventHandler<string>? OnUpdated;

    public void NotifyUpdated(string propertyName)
    {
        OnUpdated?.Invoke(this, propertyName);
    }

    public void SetFrom(IntentGraphModConfig config)
    {
        ToggleIntentGraphKey = config.ToggleIntentGraphKey;
        ShowMonsterMoveNames = config.ShowMonsterMoveNames;
        ShowMoveDetail = config.ShowMoveDetail;
        UseAnimatedIntentIcon = config.UseAnimatedIntentIcon;
        ShowCurrentMove = config.ShowCurrentMove;
        PinableIntentGraph = config.PinableIntentGraph;
        IntentGraphPosition = config.IntentGraphPosition;
        IntentGraphScale = config.IntentGraphScale;
        AttackDamageFormat = config.AttackDamageFormat;
        IntentGraphHoverTrigger = config.IntentGraphHoverTrigger;
    }
}

public enum IntentGraphPosition
{
    Default,
    TopLeft,
    TopCenter,
    TopRight,
    TopLeftOrRight,
}

public enum IntentGraphHoverTrigger
{
    Monster,
    Intents,
    Both,
}
