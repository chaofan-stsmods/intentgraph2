using Godot;
using IntentGraph2.Models;
using IntentGraph2.Patches;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntentGraph2.Scenes;

public partial class NIntentGraph : Control
{
    public const float GridSize = 80;

    private const int ArrowWidth = 10;
    private const int ArrowEndLength = 15;

    private static readonly Dictionary<IntentType, string> IntentImageResourcePath = new Dictionary<IntentType, string>
    {
        { IntentType.Attack, "res://images/packed/intents/attack/intent_attack_1.png" },
        { IntentType.Buff, "res://images/packed/intents/intent_buff.png" },
        { IntentType.Debuff, "res://images/packed/intents/intent_debuff.png" },
        { IntentType.DebuffStrong, "res://images/packed/intents/intent_debuff.png" },
        { IntentType.Defend, "res://images/packed/intents/intent_defend.png" },
        { IntentType.Escape, "res://images/packed/intents/intent_escape.png" },
        { IntentType.Heal, "res://images/packed/intents/intent_heal.png" },
        { IntentType.Hidden, "res://images/packed/intents/intent_hidden.png" },
        { IntentType.Summon, "res://images/packed/intents/summon/intent_summon_00.png" },
        { IntentType.Sleep, "res://images/packed/intents/intent_sleep.png" },
        { IntentType.Stun, "res://images/packed/intents/intent_stun.png" },
        { IntentType.StatusCard, "res://images/packed/intents/intent_status_card.png" },
        { IntentType.CardDebuff, "res://images/packed/intents/intent_card_debuff.png" },
        { IntentType.DeathBlow, "res://images/packed/intents/intent_death_blow.png" },
        { IntentType.Unknown, "res://images/packed/intents/intent_unknown.png" },
    };

    private static readonly Rect2 IconGroupLT = new Rect2(0, 0, 3, 3);
    private static readonly Rect2 IconGroupTop = new Rect2(3, 0, 26, 3);
    private static readonly Rect2 IconGroupTR = new Rect2(29, 0, 3, 3);
    private static readonly Rect2 IconGroupLeft = new Rect2(0, 3, 3, 26);
    private static readonly Rect2 IconGroupRight = new Rect2(29, 3, 3, 26);
    private static readonly Rect2 IconGroupBL = new Rect2(0, 29, 3, 3);
    private static readonly Rect2 IconGroupBottom = new Rect2(3, 29, 26, 3);
    private static readonly Rect2 IconGroupBR = new Rect2(29, 29, 3, 3);

    private static readonly Rect2 ArrowHorizontal = new Rect2(1, 0, 62, 10);
    private static readonly Rect2 ArrowVertical = new Rect2(65, 1, 10, 62);
    private static readonly Rect2 ArrowDR = new Rect2(0, 11, 10, 10);
    private static readonly Rect2 ArrowDL = new Rect2(11, 11, 10, 10);
    private static readonly Rect2 ArrowUR = new Rect2(0, 22, 10, 10);
    private static readonly Rect2 ArrowUL = new Rect2(11, 22, 10, 10);
    private static readonly Rect2 ArrowU = new Rect2(91, 0, 20, 15);
    private static readonly Rect2 ArrowD = new Rect2(91, 35, 20, 15);
    private static readonly Rect2 ArrowR = new Rect2(111, 15, 15, 20);
    private static readonly Rect2 ArrowL = new Rect2(76, 15, 15, 20);

    private Texture2D arrowTexture;
    private Texture2D groupBorderTexture;
    private Dictionary<string, Texture2D> intentTextures = new Dictionary<string, Texture2D>();
    private Font font;

    private Graph graph;

    public Graph Graph
    {
        get => graph;
        set
        {
            graph = value;
            CustomMinimumSize = new Vector2(GridSize * graph.Width, GridSize * graph.Height);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        font = ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
    }

    public override void _Input(InputEvent evt)
    {
        if (evt is InputEventKey evtKey && evtKey.Keycode == Key.F1 && evtKey.IsPressed())
        {
            ShowIntentGraphPatches.ToggleIntentGraphVisibility();
        }
    }

    public override void _Draw()
    {
        if (graph == null)
        {
            return;
        }

        foreach (var icon in graph.Icons ?? Enumerable.Empty<Icon>())
        {
            DrawIcon(icon);
        }

        foreach (var iconGroup in graph.IconGroups ?? Enumerable.Empty<IconGroup>())
        {
            DrawIconGroup(iconGroup);
        }

        foreach (var arrow in graph.Arrows ?? Enumerable.Empty<Arrow>())
        {
            DrawArrow(arrow);
        }

        foreach (var label in graph.Labels ?? Enumerable.Empty<Models.Label>())
        {
            DrawLabel(label);
        }
    }

    private void DrawIcon(Icon icon)
    {
        DrawIconIntent(icon);

        var text = string.Empty;
        if (!string.IsNullOrEmpty(icon.ValueText))
        {
            text = icon.ValueText;
        }
        else if (icon.Value.HasValue)
        {
            text = icon.Times > 1 ? $"{icon.Value}x{icon.Times}" : $"{icon.Value}";
        }

        if (!string.IsNullOrEmpty(text))
        {
            var textPosition = new Vector2(icon.X * GridSize + 12, icon.Y * GridSize + 71);
            DrawStringOutline(font, textPosition, text, fontSize: 22, size: 16, modulate: new Color(0, 0, 0, 0.5f));
            DrawString(font, textPosition, text, fontSize: 22);
        }
    }

    private void DrawIconIntent(Icon icon)
    {
        if (icon.IntentType == IntentType.Attack)
        {
            int damage = (icon.Value ?? 0) * icon.Times;
            int imageIndex = damage switch
            {
                < 5 => 1,
                < 10 => 2,
                < 20 => 3,
                < 40 => 4,
                _ => 5,
            };

            var textureKey = IntentType.Attack.ToString() + imageIndex;
            if (!intentTextures.TryGetValue(textureKey, out var texture))
            {
                texture = intentTextures[textureKey] = ResourceLoader.Load<Texture2D>($"res://images/packed/intents/attack/intent_attack_{imageIndex}.png");
            }

            DrawTextureRect(texture, new Rect2(icon.X * GridSize + 4, icon.Y * GridSize + 4, 72, 72), false);
        }
        else
        {
            if (!intentTextures.TryGetValue(icon.IntentType.ToString(), out var texture))
            {
                texture = intentTextures[icon.IntentType.ToString()] = ResourceLoader.Load<Texture2D>(IntentImageResourcePath[icon.IntentType]);
            }

            DrawTextureRect(texture, new Rect2(icon.X * GridSize + 4, icon.Y * GridSize, 72, 72), false);
        }
    }

    private void DrawArrow(Arrow arrow)
    {
        arrowTexture ??= ResourceLoader.Load<Texture2D>("res://intentgraph2/images/ui/arrow.png");

        var path = arrow.Path;

        if (path.Length <= 3)
        {
            return;
        }

        bool isHorizontal = path[0] == 0;
        float arrowX = path[1];
        float arrowY = path[2];
        float dx, dy;
        int direction = -1; // U, R, D, L
        for (int i = 3; i < path.Length; i++)
        {
            bool isStart = i == 3;
            bool isEnd = i == path.Length - 1;
            float nextArrowX = isHorizontal ? path[i] : arrowX;
            float nextArrowY = isHorizontal ? arrowY : path[i];
            int nextDirection;
            dy = (arrowY * GridSize - ArrowWidth / 2f);
            dx = (arrowX * GridSize - ArrowWidth / 2f);
            if (isHorizontal)
            {
                bool isRight = nextArrowX > arrowX;
                nextDirection = isRight ? 1 : 3;
                int startDistance = (isStart ? 0 : (isRight ? 1 : -1)) * ArrowWidth / 2;
                int endDistance = (isEnd ? ArrowEndLength : ArrowWidth / 2) * (isRight ? -1 : 1);
                float dxs = (arrowX * GridSize + startDistance);
                float dxe = (nextArrowX * GridSize + endDistance);
                float len = Math.Abs(dxs - dxe);
                DrawTextureRectRegion(arrowTexture, new Rect2(Math.Min(dxs, dxe), dy, len, ArrowWidth), ArrowHorizontal);
            }
            else
            {
                bool isDown = nextArrowY > arrowY;
                nextDirection = isDown ? 2 : 0;
                int startDistance = (isStart ? 0 : (isDown ? 1 : -1)) * ArrowWidth / 2;
                int endDistance = (isEnd ? ArrowEndLength : ArrowWidth / 2) * (isDown ? -1 : 1);
                float dys = (arrowY * GridSize + startDistance);
                float dye = (nextArrowY * GridSize + endDistance);
                float len = Math.Abs(dys - dye);
                DrawTextureRectRegion(arrowTexture, new Rect2(dx, Math.Min(dys, dye), ArrowWidth, len), ArrowVertical);
            }

            if (!isStart)
            {
                if ((direction == 2 && nextDirection == 1) || (direction == 3 && nextDirection == 0))
                {
                    DrawTextureRectRegion(arrowTexture, new Rect2(dx, dy, ArrowWidth, ArrowWidth), ArrowUR);
                }
                else if ((direction == 2 && nextDirection == 3) || (direction == 1 && nextDirection == 0))
                {
                    DrawTextureRectRegion(arrowTexture, new Rect2(dx, dy, ArrowWidth, ArrowWidth), ArrowUL);
                }
                else if ((direction == 0 && nextDirection == 3) || (direction == 1 && nextDirection == 2))
                {
                    DrawTextureRectRegion(arrowTexture, new Rect2(dx, dy, ArrowWidth, ArrowWidth), ArrowDL);
                }
                else if ((direction == 0 && nextDirection == 1) || (direction == 3 && nextDirection == 2))
                {
                    DrawTextureRectRegion(arrowTexture, new Rect2(dx, dy, ArrowWidth, ArrowWidth), ArrowDR);
                }
            }

            isHorizontal = !isHorizontal;
            arrowX = nextArrowX;
            arrowY = nextArrowY;
            direction = nextDirection;
        }

        dy = arrowY * GridSize;
        dx = arrowX * GridSize;
        switch (direction)
        {
            case 0:
                DrawTextureRectRegion(arrowTexture, new Rect2(dx - ArrowU.Size.X / 2f, dy, ArrowU.Size.X, ArrowU.Size.Y), ArrowU);
                break;
            case 1:
                DrawTextureRectRegion(arrowTexture, new Rect2(dx - ArrowEndLength, dy - ArrowR.Size.Y / 2f, ArrowR.Size.X, ArrowR.Size.Y), ArrowR);
                break;
            case 2:
                DrawTextureRectRegion(arrowTexture, new Rect2(dx - ArrowD.Size.X / 2f, dy - ArrowEndLength, ArrowD.Size.X, ArrowD.Size.Y), ArrowD);
                break;
            case 3:
                DrawTextureRectRegion(arrowTexture, new Rect2(dx, dy - ArrowL.Size.Y / 2f, ArrowL.Size.X, ArrowL.Size.Y), ArrowL);
                break;
        }
    }

    private void DrawLabel(Models.Label label)
    {
        var text = label.Text;
        var fontSize = 18;
        if (!string.IsNullOrEmpty(text))
        {
            var textPosition = new Vector2(label.X * GridSize, label.Y * GridSize);
            if (label.Align == "right")
            {
                var textSize = font.GetStringSize(text, fontSize: fontSize);
                textPosition.X -= textSize.X;
            }
            else if (label.Align != "left")
            {
                var textSize = font.GetStringSize(text, fontSize: fontSize);
                textPosition.X -= textSize.X / 2;
            }

            DrawStringOutline(font, textPosition, text, fontSize: fontSize, size: 12, modulate: new Color(0, 0, 0, 0.5f));
            DrawString(font, textPosition, text, fontSize: fontSize);
        }
    }

    private void DrawIconGroup(IconGroup iconGroup)
    {
        groupBorderTexture ??= ResourceLoader.Load<Texture2D>("res://intentgraph2/images/ui/groupborder.png");

        var position = new Vector2(iconGroup.X * GridSize, iconGroup.Y * GridSize);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position, new Vector2(3, 3)), IconGroupLT);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(3, 0), new Vector2(iconGroup.Width * GridSize - 6, 3)), IconGroupTop);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(iconGroup.Width * GridSize - 3, 0), new Vector2(3, 3)), IconGroupTR);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(0, 3), new Vector2(3, iconGroup.Height * GridSize - 6)), IconGroupLeft);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(iconGroup.Width * GridSize - 3, 3), new Vector2(3, iconGroup.Height * GridSize - 6)), IconGroupRight);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(0, iconGroup.Height * GridSize - 3), new Vector2(3, 3)), IconGroupBL);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(3, iconGroup.Height * GridSize - 3), new Vector2(iconGroup.Width * GridSize - 6, 3)), IconGroupBottom);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(iconGroup.Width * GridSize - 3, iconGroup.Height * GridSize - 3), new Vector2(3, 3)), IconGroupBR);
    }
}
