namespace IntentGraph2.Utils.GraphGenerator;
internal class MonsterStateNodeLabel
{
    public LabelType Type { get; set; }

    public required string Text { get; set; }

    public bool IsTextGenerated { get; set; }

    // 0 means no limit
    public int MaxRepeat { get; set; }

    // To calculate probability
    public float Weight { get; set; }

    // 0 means no cooldown
    public int Cooldown { get; set; }

    public bool UseOnlyOnce { get; set; }

    internal enum LabelType
    {
        Unknown = 0,
        Random,
        Condition,
    }
}
