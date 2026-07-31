using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace IntentGraph2.Scenes;

public partial class NIgArrowButton : NGoldArrowButton
{
    private Texture2D? texture;

    [Export]
    public Texture2D? Texture
    {
        get => texture;
        set
        {
            texture = value;
            if (_icon != null)
            {
                _icon.Texture = texture;
            }
        }
    }

    public override void _Ready()
    {
        base._Ready();
        if (texture != null)
        {
            _icon.Texture = texture;
        }
    }
}
