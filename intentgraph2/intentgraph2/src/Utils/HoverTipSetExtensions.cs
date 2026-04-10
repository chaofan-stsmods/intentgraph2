using Godot;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using System;

namespace IntentGraph2.Utils;

public static class HoverTipSetExtensions
{
    public static Control GetTextHoverTipContainer(this NHoverTipSet hoverTipSet)
    {
        return hoverTipSet.GetType().GetField("_textHoverTipContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(hoverTipSet) is Control control
            ? control
            : throw new Exception("Failed to get _textHoverTipContainer from hoverTipSet");
    }
}
