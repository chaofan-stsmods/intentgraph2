using Godot;
using IntentGraph2.Utils;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace IntentGraph2.Scenes;

public partial class NIntentGraphPanel : MarginContainer
{
    private bool dragging = false;
    private Vector2 dragOffset = Vector2.Zero;
    private Button? pinButton;
    private Button? closeButton;

    public NCreature? NCreature { get; set; }

    public bool Pinned => pinButton?.ButtonPressed == true;

    public override void _Ready()
    {
        if (IntentGraphMod.Config.PinableIntentGraph)
        {
            GetNode<BoxContainer>("%ButtonContainer").Show();
            MouseFilter = MouseFilterEnum.Pass;
        }

        pinButton = GetNode<Button>("%PinButton");
        closeButton = GetNode<Button>("%CloseButton");
        pinButton.Toggled += OnPinButtonToggled;
        closeButton.Pressed += OnCloseButtonPressed;
    }

    public override void _GuiInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton evtMb && evtMb.ButtonIndex == MouseButton.Left)
        {
            if (evtMb.Pressed)
            {
                MoveToFront();
                dragging = true;
                dragOffset = GetGlobalMousePosition() - GlobalPosition;
            }
            else
            {
                dragging = false;
            }
        }

        if (evt is InputEventMouseMotion && dragging)
        {
            GlobalPosition = GetGlobalMousePosition() - dragOffset;
        }
    }

    public override void _Input(InputEvent evt)
    {
        if (evt is InputEventKey evtKey && evtKey.IsPressed() && IntentGraphMod.Config.ToggleIntentGraphKey == evtKey.Keycode)
        {
            IntentGraphHost.ToggleIntentGraphVisibility();
            GetViewport().SetInputAsHandled();
        }

        if (evt is InputEventMouseButton evtMb && evtMb.ButtonIndex == MouseButton.Left && evtMb.Pressed &&
            NCreature != null && !Pinned)
        {
            var localClickPos = ((InputEventMouseButton)MakeInputLocal(evt)).Position;
            var panelRect = new Rect2(Vector2.Zero, Size);
            if (!panelRect.HasPoint(localClickPos))
            {
                IntentGraphHost.Remove(NCreature);
            }
        }
    }

    private void OnPinButtonToggled(bool toggledOn)
    {
        if (pinButton != null)
        {
            if (toggledOn)
            {
                pinButton.Icon = ResourceLoader.Load<Texture2D>("res://intentgraph2/images/ui/unpin.png");
            }
            else
            {
                pinButton.Icon = ResourceLoader.Load<Texture2D>("res://intentgraph2/images/ui/pin.png");
            }
        }
    }

    private void OnCloseButtonPressed()
    {
        if (NCreature != null)
        {
            IntentGraphHost.Remove(NCreature);
        }
    }
}
