using Godot;
using IntentGraph2.Models;
using IntentGraph2.Patches;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.TestSupport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static IntentGraph2.Scenes.NIntentGraph;

namespace IntentGraph2.Scenes;

public partial class NIntentGraphCanvas : Control
{
    private const int ArrowWidth = 10;
    private const int ArrowEndLength = 15;
    private const int AnimatedIconFrameDurationMs = 80;
    private readonly Move InitMove = new Move(IntentGraphMod.ModId + "_special_init_move_", PossiblePreviousMoveNodeIndices: []);

    private static readonly Dictionary<IntentType, string> IntentImageResourcePath = new Dictionary<IntentType, string>
    {
        { IntentType.Attack, "res://images/packed/intents/attack/intent_attack_1.png" },
        { IntentType.Buff, "res://images/packed/intents/intent_buff.png" },
        { IntentType.Debuff, "res://images/packed/intents/debuff/intent_megadebuff_01.png" },
        { IntentType.DebuffStrong, "res://images/packed/intents/debuff/intent_megadebuff_01.png" },
        { IntentType.Defend, "res://images/packed/intents/intent_defend.png" },
        { IntentType.Escape, "res://images/packed/intents/intent_escape.png" },
        { IntentType.Heal, "res://images/packed/intents/intent_heal.png" },
        { IntentType.Hidden, "res://images/packed/intents/intent_hidden.png" },
        { IntentType.Summon, "res://images/packed/intents/intent_summon.png" },
        { IntentType.Sleep, "res://images/packed/intents/intent_sleep.png" },
        { IntentType.Stun, "res://images/packed/intents/intent_stun.png" },
        { IntentType.StatusCard, "res://images/packed/intents/intent_status_card.png" },
        { IntentType.CardDebuff, "res://images/packed/intents/intent_card_debuff.png" },
        { IntentType.DeathBlow, "res://images/packed/intents/intent_death_blow.png" },
        { IntentType.Unknown, "res://images/packed/intents/intent_unknown.png" },
    };

    private static readonly Dictionary<IntentType, string> IntentImageAnimationResourcePath = new Dictionary<IntentType, string>
    {
        { IntentType.Buff, "res://images/packed/intents/buff/intent_buff_{0:00}.png" },
        { IntentType.Debuff, "res://images/packed/intents/debuff/intent_megadebuff_{0:00}.png" },
        { IntentType.DebuffStrong, "res://images/packed/intents/debuff/intent_megadebuff_{0:00}.png" },
        { IntentType.Defend, "res://images/packed/intents/defend/intent_defend_{0:00}.png" },
        { IntentType.Escape, "res://images/packed/intents/escape/intent_escape_{0:00}.png" },
        { IntentType.Heal, "res://images/packed/intents/heal/intent_heal_{0:00}.png" },
        { IntentType.Summon, "res://images/packed/intents/summon/intent_summon_{0:00}.png" },
        { IntentType.Sleep, "res://images/packed/intents/sleep/intent_sleep_{0:00}.png" },
        { IntentType.Stun, "res://images/packed/intents/stun/intent_stunned_{0:00}.png" },
        { IntentType.StatusCard, "res://images/packed/intents/status/intent_statuscard_{0:00}.png" },
        { IntentType.CardDebuff, "res://images/packed/intents/card_debuff/intent_carddebuff_{0:00}.png" },
        { IntentType.Unknown, "res://images/packed/intents/unknown/intent_unknown_{0:00}.png" },
    };

    private static readonly Dictionary<IntentType, int> IntentImageAnimationFrameCounts = new Dictionary<IntentType, int>
    {
        { IntentType.Buff, 30 },
        { IntentType.Debuff, 11 },
        { IntentType.DebuffStrong, 11 },
        { IntentType.Defend, 45 },
        { IntentType.Escape, 40 },
        { IntentType.Heal, 45 },
        { IntentType.Summon, 25 },
        { IntentType.Sleep, 16 },
        { IntentType.Stun, 16 },
        { IntentType.StatusCard, 19 },
        { IntentType.CardDebuff, 15 },
        { IntentType.Unknown, 30 },
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

    private Texture2D? arrowTexture;
    private Texture2D? groupBorderTexture;
    private Texture2D? glowTexture;
    private Dictionary<string, Texture2D> intentTextures = new Dictionary<string, Texture2D>();
    private Font? font;
    private Font? labelFont;
    private Color glowColor;

    private Graph? graph;
    private bool hasAnimatedIcons;
    private int previousStateLogLength;
    private List<Move> glowingMoves = new();

    public Graph? Graph
    {
        get => graph;
        set
        {
            graph = value;
            hasAnimatedIcons = graph?.Moves?.Any(m => m.Icons?.Any(icon => IntentImageAnimationFrameCounts.ContainsKey(icon.IntentType)) == true) == true;
            CustomMinimumSize = new Vector2(GridSize * graph?.Width ?? GridSize, GridSize * graph?.Height ?? GridSize);
            QueueRedraw();
        }
    }

    public MonsterModel? Monster { get; set; }

    public bool AnimatedIcons { get; set; }

    public bool ShowCurrentMove { get; set; }

    public override void _Ready()
    {
        this.labelFont = this.font = ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");

        if (LocManager.Instance.Language == "zhs" || LocManager.Instance.Language == "zht")
        {
            this.labelFont = ResourceLoader.Load<Font>("res://intentgraph2/themes/kreon_bold_glyph_space_one_zhs.tres");
        }
        else if (!Engine.IsEditorHint() && !TestMode.IsOn && FontManager.NeedsFontSubstitution(LocManager.Instance.Language))
        {
            var font = new FontVariation();
            var standardFont = FontManager.GetSubstituteFont(LocManager.Instance.Language, FontType.Bold);
            if (standardFont != null)
            {
                font.BaseFont = standardFont;
                font.Fallbacks.Add(ResourceLoader.Load<Font>("res://intentgraph2/images/ui/icon.png"));
                this.labelFont = font;
            }
        }
        else
        {
            var font = new FontVariation();
            font.BaseFont = this.labelFont;
            font.Fallbacks.Add(ResourceLoader.Load<Font>("res://intentgraph2/images/ui/icon.png"));
            this.labelFont = font;
        }
    }

    public override void _Process(double delta)
    {
        if ((hasAnimatedIcons || ShowCurrentMove) &&
            graph != null && Visible && AnimatedIcons)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (graph == null)
        {
            return;
        }

        if (ShowCurrentMove)
        {
            var glowOpacity = AnimatedIcons ? 0.3f + 0.4f * Mathf.Sin(Time.GetTicksMsec() / 1000f * Mathf.Pi) : 0.5f;
            glowColor = new Color(1, 1, 1, glowOpacity);
        }

        if (ShowCurrentMove)
        {
            DrawGlow(graph.Moves);
        }

        foreach (var icon in graph.Icons ?? Enumerable.Empty<Icon>())
        {
            DrawIcon(icon);
        }

        foreach (var move in graph.Moves ?? Enumerable.Empty<Move>())
        {
            DrawMove(move);
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

    private void DrawGlow(List<Move> moves)
    {
        if (moves == null || Monster?.MoveStateMachine == null)
        {
            return;
        }

        var stateMachine = Monster.MoveStateMachine;
        if (!StateLogPatches.FullStateLog.TryGetValue(stateMachine, out var fullStateLog))
        {
            return;
        }

        if (previousStateLogLength == fullStateLog.Count)
        {
            foreach (var move in glowingMoves)
            {
                DrawMoveGlow(move);
            }
            return;
        }

        previousStateLogLength = fullStateLog.Count;
        glowingMoves.Clear();

        var lastIndex = 1;
        var possibleMoves = moves.Select<Move, (Move? curr, Move final)>(m => (m, m)).ToList();
        while (fullStateLog.Count >= lastIndex)
        {
            var state = fullStateLog[^lastIndex];
            var filteredPossibleMoves = possibleMoves.Where(m => m.curr == null || m.curr.Ids?.Contains(state.Id) == true).ToList();
            if (filteredPossibleMoves.Count == 1)
            {
                glowingMoves.Add(filteredPossibleMoves[0].final);
                DrawMoveGlow(filteredPossibleMoves[0].final);
                return;
            }

            if (filteredPossibleMoves.Count == 0)
            {
                break;
            }

            possibleMoves = filteredPossibleMoves.SelectMany(m =>
                m.curr == null ?
                    [m] : // curr == null means any, no change.
                    (m.curr.PossiblePreviousMoveNodeIndices == null ?
                        [(null, m.final)] :
                        m.curr.PossiblePreviousMoveNodeIndices.Select<int?, (Move? curr, Move final)>(i =>
                            i == null ? (InitMove, m.final) : (moves[i.Value], m.final))) // i == null means initial
            ).ToList();
            lastIndex++;
        }

        foreach (var move in possibleMoves.Where(m => m.curr == null || m.curr == InitMove).Select(m => m.final).Distinct())
        {
            glowingMoves.Add(move);
            DrawMoveGlow(move);
        }
    }

    private void DrawMoveGlow(Move move)
    {
        glowTexture ??= ResourceLoader.Load<Texture2D>("res://intentgraph2/images/ui/glow.png");

        foreach (var icon in move.Icons ?? Enumerable.Empty<Icon>())
        {
            var expand = 0.25f * GridSize;
            DrawTextureRect(glowTexture, new Rect2(icon.X * GridSize - expand, icon.Y * GridSize - expand, GridSize + 2 * expand, GridSize + 2 * expand), false, glowColor);
        }
    }

    private void DrawMove(Move move)
    {
        foreach (var icon in move.Icons ?? Enumerable.Empty<Icon>())
        {
            DrawIcon(icon);
        }
    }

    private void DrawIcon(Icon icon)
    {
        DrawIconIntent(icon);

        var text = string.Empty;
        var valueText = !string.IsNullOrEmpty(icon.ValueText) ? icon.ValueText.Replace("{}", icon.Value?.ToString()) : (icon.Value?.ToString() ?? string.Empty);
        if (!string.IsNullOrEmpty(valueText))
        {
            if (icon.Times <= 1 && string.IsNullOrEmpty(icon.TimesText))
            {
                text = valueText;
            }
            else
            {
                var timesText = !string.IsNullOrEmpty(icon.TimesText) ? icon.TimesText.Replace("{}", icon.Times.ToString()) : icon.Times.ToString();
                text = $"{valueText}x{timesText}";
            }
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
            if (!TryGetAnimatedIntentTexture(icon.IntentType, out var texture)
                && !intentTextures.TryGetValue(icon.IntentType.ToString(), out texture))
            {
                texture = intentTextures[icon.IntentType.ToString()] = ResourceLoader.Load<Texture2D>(IntentImageResourcePath[icon.IntentType]);
            }

            DrawTextureRect(texture, new Rect2(icon.X * GridSize + 4, icon.Y * GridSize, 72, 72), false);
        }
    }

    private bool TryGetAnimatedIntentTexture(IntentType intentType, out Texture2D? texture)
    {
        texture = null;

        if (!AnimatedIcons
            || !IntentImageAnimationFrameCounts.TryGetValue(intentType, out int frameCount)
            || frameCount <= 0
            || !IntentImageAnimationResourcePath.TryGetValue(intentType, out string? animationPathFormat))
        {
            return false;
        }

        var frame = (int)((Time.GetTicksMsec() / AnimatedIconFrameDurationMs) % (ulong)frameCount);
        var textureKey = $"{intentType}_{frame}";
        if (!intentTextures.TryGetValue(textureKey, out texture))
        {
            texture = intentTextures[textureKey] = ResourceLoader.Load<Texture2D>(string.Format(animationPathFormat, frame));
        }

        return texture != null;
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
        Debug.Assert(labelFont != null, "labelFont is not initialized");

        var text = label.Text;
        var fontSize = label.FontSize;
        if (!string.IsNullOrEmpty(text))
        {
            var textPosition = new Vector2(label.X * GridSize, label.Y * GridSize);
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                var linePosition = textPosition;
                if (label.Align == "right")
                {
                    var textSize = labelFont.GetStringSize(text, fontSize: fontSize);
                    linePosition.X -= textSize.X;
                }
                else if (label.Align != "left")
                {
                    var textSize = labelFont.GetStringSize(text, fontSize: fontSize);
                    linePosition.X -= textSize.X / 2;
                }

                DrawStringOutline(labelFont, linePosition, line, fontSize: fontSize, size: 12, modulate: new Color(0, 0, 0, 0.5f));
                DrawString(labelFont, linePosition, line, fontSize: fontSize);

                textPosition.Y += fontSize + LabelLinePadding;
            }
        }
    }

    private void DrawIconGroup(IconGroup iconGroup)
    {
        groupBorderTexture ??= ResourceLoader.Load<Texture2D>("res://intentgraph2/images/ui/groupborder.png");

        var x = iconGroup.X * GridSize;
        var y = iconGroup.Y * GridSize;
        var width = iconGroup.Width * GridSize;
        var height = iconGroup.Height * GridSize;
        var position = new Vector2(x, y);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position, new Vector2(3, 3)), IconGroupLT);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(3, 0), new Vector2(width - 6, 3)), IconGroupTop);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(width - 3, 0), new Vector2(3, 3)), IconGroupTR);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(0, 3), new Vector2(3, height - 6)), IconGroupLeft);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(width - 3, 3), new Vector2(3, height - 6)), IconGroupRight);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(0, height - 3), new Vector2(3, 3)), IconGroupBL);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(3, height - 3), new Vector2(width - 6, 3)), IconGroupBottom);
        DrawTextureRectRegion(groupBorderTexture, new Rect2(position + new Vector2(width - 3, height - 3), new Vector2(3, 3)), IconGroupBR);
    }
}
