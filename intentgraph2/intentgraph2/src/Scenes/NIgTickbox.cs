using Godot;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace IntentGraph2.Scenes;

public partial class NIgTickbox : NTickbox
{
    public Label? Label { get; set; }

    public override void _Ready()
    {
        ConnectSignals();
        Label = GetNode<Label>("%Label");
        Label.ApplyLocaleFontSubstitution(FontType.Regular, "font");
    }
}
