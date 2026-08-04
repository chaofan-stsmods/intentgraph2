using IntentGraph2.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace IntentGraph2.Utils.GraphGenerator;

internal static class MoveDetailResolver
{
    private const string PowerCmdTypeName = "MegaCrit.Sts2.Core.Commands.PowerCmd";
    private const string CardPileCmdTypeName = "MegaCrit.Sts2.Core.Commands.CardPileCmd";
    private const string ModelDbTypeName = "MegaCrit.Sts2.Core.Models.ModelDb";

    private static readonly FieldInfo? OnPerformField = typeof(MoveState).GetField("_onPerform", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly ConditionalWeakTable<MoveState, List<RawMoveDetail>> RawMoveDetailCache = new();
    private static readonly Dictionary<Type, PowerModel?> PowerModelCache = new();
    private static readonly Dictionary<Type, CardModel?> CardModelCache = new();
    private static readonly object ModelCacheLock = new();

    private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

    static MoveDetailResolver()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var value = unchecked((ushort)opCode.Value);
            if (value < 0x100)
            {
                SingleByteOpCodes[value] = opCode;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                MultiByteOpCodes[value & 0xff] = opCode;
            }
        }
    }

    public static List<ResolvedIntentIcon> ResolveIntentIcons(MoveState moveState)
    {
        var intents = moveState.Intents;
        var originalIcons = intents
            .Select((intent, index) => new ResolvedIntentIcon(intent, index))
            .ToList();

        if (!IntentGraphMod.Config.ShowMoveDetail || intents.Count == 0)
        {
            return originalIcons;
        }

        try
        {

            var rawContents = RawMoveDetailCache.GetValue(moveState, ScanMove);
            if (rawContents.Count == 0)
            {
                return originalIcons;
            }

            var contentByIntent = new Dictionary<int, List<RawMoveDetail>>();
            var powerIntentIndices = Enumerable.Range(0, intents.Count)
                .Where(i => IsPowerIntent(intents[i].IntentType))
                .ToList();
            AssignPowers(
                rawContents.Where(content => content.Type == MoveDetailIconType.Power).ToList(),
                powerIntentIndices,
                intents,
                contentByIntent);

            AssignByPosition(
                rawContents.Where(content => content.Type == MoveDetailIconType.Status).ToList(),
                Enumerable.Range(0, intents.Count).Where(i => intents[i].IntentType == IntentType.StatusCard).ToList(),
                contentByIntent);

            var result = new List<ResolvedIntentIcon>();
            for (var i = 0; i < intents.Count; i++)
            {
                if (!IsReplaceableIntent(intents[i].IntentType)
                    || !contentByIntent.TryGetValue(i, out var contents)
                    || contents.Count == 0)
                {
                    result.Add(originalIcons[i]);
                    continue;
                }

                result.AddRange(contents.Select(content => CreateResolvedIcon(intents[i], i, content)));
            }

            return result;
        }
        catch (Exception ex)
        {
            IgLogger.Warn($"Unable to resolve action contents for move '{moveState.Id}': {ex}");
            return originalIcons;
        }
    }

    private static bool IsReplaceableIntent(IntentType intentType)
    {
        return IsPowerIntent(intentType) || intentType == IntentType.StatusCard;
    }

    private static bool IsPowerIntent(IntentType intentType)
    {
        return intentType is IntentType.Buff or IntentType.Debuff or IntentType.DebuffStrong or IntentType.CardDebuff;
    }

    private static void AssignPowers(
        List<RawMoveDetail> powers,
        List<int> intentIndices,
        IReadOnlyList<AbstractIntent> intents,
        Dictionary<int, List<RawMoveDetail>> destination)
    {
        if (powers.Count == 0 || intentIndices.Count == 0)
        {
            return;
        }

        if (intentIndices.Count == 1)
        {
            AddRange(destination, intentIndices[0], powers);
            return;
        }

        // Moves that expose one abstract intent per power (for example, stealing a stat)
        // preserve the source order even when a dynamic amount prevents type classification.
        if (powers.Count == intentIndices.Count)
        {
            for (var i = 0; i < powers.Count; i++)
            {
                Add(destination, intentIndices[i], powers[i]);
            }
            return;
        }

        var nextIndexByIntentType = new Dictionary<IntentType, int>();
        foreach (var power in powers)
        {
            var expectedIntentTypes = GetCompatibleIntentTypes(power);
            var candidates = intentIndices
                .Where(index => expectedIntentTypes.Contains(intents[index].IntentType))
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = intentIndices
                    .Where(index => intents[index].IntentType == IntentType.CardDebuff)
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                candidates = intentIndices;
            }

            var candidateType = intents[candidates[0]].IntentType;
            var next = nextIndexByIntentType.GetValueOrDefault(candidateType);
            var destinationIndex = candidates[next % candidates.Count];
            nextIndexByIntentType[candidateType] = next + 1;
            Add(destination, destinationIndex, power);
        }
    }

    private static HashSet<IntentType> GetCompatibleIntentTypes(RawMoveDetail power)
    {
        var powerModel = GetPowerModel(power.ModelType);
        var powerType = powerModel?.Type ?? PowerType.None;
        if (power.Amount < 0)
        {
            powerType = powerType switch
            {
                PowerType.Buff => PowerType.Debuff,
                PowerType.Debuff => PowerType.Buff,
                _ => powerType,
            };
        }

        return powerType switch
        {
            PowerType.Buff => [IntentType.Buff],
            PowerType.Debuff => [IntentType.Debuff, IntentType.DebuffStrong],
            _ => [],
        };
    }

    private static void AssignByPosition(
        List<RawMoveDetail> contents,
        List<int> intentIndices,
        Dictionary<int, List<RawMoveDetail>> destination)
    {
        if (contents.Count == 0 || intentIndices.Count == 0)
        {
            return;
        }

        if (intentIndices.Count == 1)
        {
            AddRange(destination, intentIndices[0], contents);
            return;
        }

        if (contents.Count == intentIndices.Count)
        {
            for (var i = 0; i < contents.Count; i++)
            {
                Add(destination, intentIndices[i], contents[i]);
            }
            return;
        }

        for (var i = 0; i < contents.Count; i++)
        {
            Add(destination, intentIndices[Math.Min(i, intentIndices.Count - 1)], contents[i]);
        }
    }

    private static void AddRange(Dictionary<int, List<RawMoveDetail>> destination, int intentIndex, IEnumerable<RawMoveDetail> contents)
    {
        foreach (var content in contents)
        {
            Add(destination, intentIndex, content);
        }
    }

    private static void Add(Dictionary<int, List<RawMoveDetail>> destination, int intentIndex, RawMoveDetail content)
    {
        if (!destination.TryGetValue(intentIndex, out var values))
        {
            values = new List<RawMoveDetail>();
            destination[intentIndex] = values;
        }
        values.Add(content);
    }

    private static ResolvedIntentIcon CreateResolvedIcon(AbstractIntent intent, int intentIndex, RawMoveDetail content)
    {
        return content.Type switch
        {
            MoveDetailIconType.Power => new ResolvedIntentIcon(
                intent,
                intentIndex,
                GetPowerModel(content.ModelType)?.IconPath ?? string.Empty,
                MoveDetailIconType.Power,
                content.Amount),
            MoveDetailIconType.Status => new ResolvedIntentIcon(
                intent,
                intentIndex,
                GetCardModel(content.ModelType)?.PortraitPath ?? string.Empty,
                MoveDetailIconType.Status,
                intent is StatusIntent statusIntent ? statusIntent.CardCount : content.Amount),
            _ => new ResolvedIntentIcon(intent, intentIndex),
        };
    }

    private static PowerModel? GetPowerModel(Type powerType)
    {
        lock (ModelCacheLock)
        {
            if (!PowerModelCache.TryGetValue(powerType, out var power))
            {
                try
                {
                    power = ModelDb.DebugPower(powerType);
                }
                catch
                {
                }
                PowerModelCache[powerType] = power;
            }
            return power;
        }
    }

    private static CardModel? GetCardModel(Type cardType)
    {
        lock (ModelCacheLock)
        {
            if (!CardModelCache.TryGetValue(cardType, out var card))
            {
                try
                {
                    card = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(cardType));
                }
                catch
                {
                }
                CardModelCache[cardType] = card;
            }
            return card;
        }
    }

    private static List<RawMoveDetail> ScanMove(MoveState moveState)
    {
        var result = new List<RawMoveDetail>();
        try
        {
            if (OnPerformField?.GetValue(moveState) is not Delegate onPerform)
            {
                return result;
            }

            ScanMethod(onPerform.Method, onPerform.Target, result, new HashSet<MethodInfo>());
        }
        catch (Exception ex)
        {
            IgLogger.Debug($"Unable to resolve action contents for move '{moveState.Id}': {ex.Message}");
        }
        return result;
    }

    private static void ScanMethod(
        MethodInfo method,
        object? evaluationTarget,
        List<RawMoveDetail> destination,
        HashSet<MethodInfo> visited)
    {
        if (!visited.Add(method))
        {
            return;
        }

        var stateMachineType = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (stateMachineType != null)
        {
            var moveNext = stateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (moveNext != null && moveNext != method)
            {
                ScanMethod(moveNext, evaluationTarget, destination, visited);
                return;
            }
        }

        var instructions = ReadInstructions(method);
        var powerCandidates = new List<(Type type, bool used)>();
        var sawNonGenericPowerApply = false;
        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].Operand is not MethodInfo calledMethod)
            {
                continue;
            }

            var declaringTypeName = calledMethod.DeclaringType?.FullName;
            if (declaringTypeName == ModelDbTypeName
                && calledMethod.Name == "Power"
                && calledMethod.IsGenericMethod)
            {
                var genericArguments = calledMethod.GetGenericArguments();
                if (genericArguments.Length > 0)
                {
                    powerCandidates.Add((genericArguments[0], false));
                }
                continue;
            }

            if (declaringTypeName == PowerCmdTypeName && calledMethod.Name == "Apply")
            {
                if (calledMethod.IsGenericMethod)
                {
                    var genericArguments = calledMethod.GetGenericArguments();
                    if (genericArguments.Length > 0)
                    {
                        destination.Add(new RawMoveDetail(
                            MoveDetailIconType.Power,
                            genericArguments[0],
                            ScanDecimalAmountBefore(instructions, i, evaluationTarget)));
                    }
                }
                else
                {
                    sawNonGenericPowerApply = true;
                    for (var candidateIndex = powerCandidates.Count - 1; candidateIndex >= 0; candidateIndex--)
                    {
                        var candidate = powerCandidates[candidateIndex];
                        if (candidate.used)
                        {
                            continue;
                        }

                        destination.Add(new RawMoveDetail(
                            MoveDetailIconType.Power,
                            candidate.type,
                            ScanDecimalAmountBefore(instructions, i, evaluationTarget)));
                        powerCandidates[candidateIndex] = (candidate.type, true);
                        break;
                    }
                }
                continue;
            }

            if (declaringTypeName == CardPileCmdTypeName
                && calledMethod.Name == "AddToCombatAndPreview"
                && calledMethod.IsGenericMethod)
            {
                var genericArguments = calledMethod.GetGenericArguments();
                if (genericArguments.Length > 0)
                {
                    destination.Add(new RawMoveDetail(
                        MoveDetailIconType.Status,
                        genericArguments[0],
                        ScanIntBefore(instructions, i)));
                }
                continue;
            }
        }

        if (!sawNonGenericPowerApply)
        {
            return;
        }

        foreach (var candidate in powerCandidates.Where(candidate => !candidate.used))
        {
            destination.Add(new RawMoveDetail(MoveDetailIconType.Power, candidate.type, null));
        }
    }

    private static int? ScanDecimalAmountBefore(
        IReadOnlyList<IlInstruction> instructions,
        int callIndex,
        object? evaluationTarget)
    {
        var start = Math.Max(0, callIndex - 40);
        for (var i = callIndex - 1; i >= start; i--)
        {
            if (instructions[i].Operand is FieldInfo decimalField
                && decimalField.DeclaringType == typeof(decimal)
                && decimalField.IsStatic)
            {
                try
                {
                    if (TryConvertToInt(decimalField.GetValue(null), out var fieldAmount))
                    {
                        return fieldAmount;
                    }
                }
                catch
                {
                    // Keep scanning: malformed or inaccessible metadata should not hide the intent.
                }
            }

            if (instructions[i].Operand is not MethodBase method
                || method.DeclaringType != typeof(decimal)
                || method.GetParameters().Length != 1
                || method.GetParameters()[0].ParameterType != typeof(int)
                || (instructions[i].OpCode != OpCodes.Newobj && method.Name != "op_Implicit"))
            {
                continue;
            }

            var expressionIndex = i - 1;
            if (TryEvaluateIntExpression(instructions, ref expressionIndex, evaluationTarget, out var amount))
            {
                return amount;
            }
        }
        return null;
    }

    private static bool TryEvaluateIntExpression(
        IReadOnlyList<IlInstruction> instructions,
        ref int index,
        object? evaluationTarget,
        out int value)
    {
        while (index >= 0 && IsTransparentNumericInstruction(instructions[index].OpCode))
        {
            index--;
        }

        if (index < 0)
        {
            value = 0;
            return false;
        }

        var instruction = instructions[index];
        if (TryGetIntConstant(instruction, out value))
        {
            index--;
            return true;
        }

        if (instruction.OpCode == OpCodes.Neg)
        {
            index--;
            if (!TryEvaluateIntExpression(instructions, ref index, evaluationTarget, out var operand))
            {
                value = 0;
                return false;
            }

            try
            {
                value = checked(-operand);
                return true;
            }
            catch (OverflowException)
            {
                value = 0;
                return false;
            }
        }

        if (IsSupportedBinaryNumericInstruction(instruction.OpCode))
        {
            index--;
            if (!TryEvaluateIntExpression(instructions, ref index, evaluationTarget, out var right)
                || !TryEvaluateIntExpression(instructions, ref index, evaluationTarget, out var left))
            {
                value = 0;
                return false;
            }

            return TryEvaluateBinary(instruction.OpCode, left, right, out value);
        }

        if (instruction.Operand is MethodInfo calledMethod
            && instruction.OpCode is var callOpCode
            && (callOpCode == OpCodes.Call || callOpCode == OpCodes.Callvirt)
            && calledMethod.IsSpecialName
            && calledMethod.Name.StartsWith("get_", StringComparison.Ordinal)
            && calledMethod.GetParameters().Length == 0)
        {
            index--;
            object? instance = null;
            if (!calledMethod.IsStatic)
            {
                if (evaluationTarget == null || calledMethod.DeclaringType?.IsInstanceOfType(evaluationTarget) != true)
                {
                    value = 0;
                    return false;
                }

                instance = evaluationTarget;
                SkipEvaluationTargetReceiver(instructions, ref index, evaluationTarget);
            }

            try
            {
                return TryConvertToInt(calledMethod.Invoke(instance, null), out value);
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        if (instruction.Operand is FieldInfo field)
        {
            index--;
            object? instance = null;
            if (!field.IsStatic)
            {
                if (evaluationTarget == null || field.DeclaringType?.IsInstanceOfType(evaluationTarget) != true)
                {
                    value = 0;
                    return false;
                }

                instance = evaluationTarget;
                SkipEvaluationTargetReceiver(instructions, ref index, evaluationTarget);
            }

            try
            {
                return TryConvertToInt(field.GetValue(instance), out value);
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        value = 0;
        return false;
    }

    private static void SkipEvaluationTargetReceiver(
        IReadOnlyList<IlInstruction> instructions,
        ref int index,
        object evaluationTarget)
    {
        if (index >= 0
            && instructions[index].OpCode == OpCodes.Ldfld
            && instructions[index].Operand is FieldInfo targetField
            && targetField.FieldType.IsInstanceOfType(evaluationTarget))
        {
            index--;
        }

        if (index >= 0 && instructions[index].OpCode == OpCodes.Ldarg_0)
        {
            index--;
        }
    }

    private static bool IsTransparentNumericInstruction(OpCode opCode)
    {
        return opCode == OpCodes.Nop
            || opCode == OpCodes.Conv_I
            || opCode == OpCodes.Conv_I1
            || opCode == OpCodes.Conv_I2
            || opCode == OpCodes.Conv_I4
            || opCode == OpCodes.Conv_I8
            || opCode == OpCodes.Conv_U
            || opCode == OpCodes.Conv_U1
            || opCode == OpCodes.Conv_U2
            || opCode == OpCodes.Conv_U4
            || opCode == OpCodes.Conv_U8;
    }

    private static bool IsSupportedBinaryNumericInstruction(OpCode opCode)
    {
        return opCode == OpCodes.Add
            || opCode == OpCodes.Add_Ovf
            || opCode == OpCodes.Add_Ovf_Un
            || opCode == OpCodes.Sub
            || opCode == OpCodes.Sub_Ovf
            || opCode == OpCodes.Sub_Ovf_Un
            || opCode == OpCodes.Mul
            || opCode == OpCodes.Mul_Ovf
            || opCode == OpCodes.Mul_Ovf_Un
            || opCode == OpCodes.Div
            || opCode == OpCodes.Div_Un
            || opCode == OpCodes.Rem
            || opCode == OpCodes.Rem_Un
            || opCode == OpCodes.And
            || opCode == OpCodes.Or
            || opCode == OpCodes.Xor
            || opCode == OpCodes.Shl
            || opCode == OpCodes.Shr
            || opCode == OpCodes.Shr_Un;
    }

    private static bool TryEvaluateBinary(OpCode opCode, int left, int right, out int value)
    {
        try
        {
            value = opCode.Value switch
            {
                var code when code == OpCodes.Add.Value || code == OpCodes.Add_Ovf.Value || code == OpCodes.Add_Ovf_Un.Value => checked(left + right),
                var code when code == OpCodes.Sub.Value || code == OpCodes.Sub_Ovf.Value || code == OpCodes.Sub_Ovf_Un.Value => checked(left - right),
                var code when code == OpCodes.Mul.Value || code == OpCodes.Mul_Ovf.Value || code == OpCodes.Mul_Ovf_Un.Value => checked(left * right),
                var code when code == OpCodes.Div.Value || code == OpCodes.Div_Un.Value => left / right,
                var code when code == OpCodes.Rem.Value || code == OpCodes.Rem_Un.Value => left % right,
                var code when code == OpCodes.And.Value => left & right,
                var code when code == OpCodes.Or.Value => left | right,
                var code when code == OpCodes.Xor.Value => left ^ right,
                var code when code == OpCodes.Shl.Value => left << right,
                var code when code == OpCodes.Shr.Value || code == OpCodes.Shr_Un.Value => left >> right,
                _ => throw new InvalidOperationException(),
            };
            return true;
        }
        catch (Exception ex) when (ex is OverflowException or DivideByZeroException or InvalidOperationException)
        {
            value = 0;
            return false;
        }
    }

    private static bool TryConvertToInt(object? input, out int value)
    {
        switch (input)
        {
            case int intValue:
                value = intValue;
                return true;
            case uint uintValue when uintValue <= int.MaxValue:
                value = (int)uintValue;
                return true;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                value = (int)longValue;
                return true;
            case decimal decimalValue when decimalValue == decimal.Truncate(decimalValue)
                                           && decimalValue is >= int.MinValue and <= int.MaxValue:
                value = (int)decimalValue;
                return true;
            case Enum enumValue:
                value = Convert.ToInt32(enumValue);
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static int? ScanIntBefore(IReadOnlyList<IlInstruction> instructions, int callIndex)
    {
        var start = Math.Max(0, callIndex - 20);
        for (var i = callIndex - 1; i >= start; i--)
        {
            if (TryGetIntConstant(instructions[i], out var value))
            {
                return value;
            }
        }
        return null;
    }

    private static bool TryGetIntConstant(IlInstruction instruction, out int value)
    {
        if (instruction.OpCode == OpCodes.Ldc_I4_M1)
        {
            value = -1;
            return true;
        }
        if (instruction.OpCode == OpCodes.Ldc_I4_0) { value = 0; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_1) { value = 1; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_2) { value = 2; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_3) { value = 3; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_4) { value = 4; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_5) { value = 5; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_6) { value = 6; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_7) { value = 7; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_8) { value = 8; return true; }
        if (instruction.OpCode is var opCode && (opCode == OpCodes.Ldc_I4 || opCode == OpCodes.Ldc_I4_S)
            && instruction.Operand is int operand)
        {
            value = operand;
            return true;
        }

        value = 0;
        return false;
    }

    private static List<IlInstruction> ReadInstructions(MethodInfo method)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        if (il == null)
        {
            return new List<IlInstruction>();
        }

        var declaringType = method.DeclaringType;
        var typeArguments = declaringType?.IsGenericType == true ? declaringType.GetGenericArguments() : Type.EmptyTypes;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
        var result = new List<IlInstruction>();
        var offset = 0;
        while (offset < il.Length)
        {
            var instructionOffset = offset;
            OpCode opCode;
            var first = il[offset++];
            if (first == 0xfe)
            {
                if (offset >= il.Length)
                {
                    break;
                }
                opCode = MultiByteOpCodes[il[offset++]];
            }
            else
            {
                opCode = SingleByteOpCodes[first];
            }

            object? operand = null;
            try
            {
                operand = ReadOperand(method.Module, il, ref offset, opCode.OperandType, typeArguments, methodArguments);
            }
            catch
            {
                break;
            }
            result.Add(new IlInstruction(instructionOffset, opCode, operand));
        }
        return result;
    }

    private static object? ReadOperand(
        Module module,
        byte[] il,
        ref int offset,
        OperandType operandType,
        Type[] typeArguments,
        Type[] methodArguments)
    {
        switch (operandType)
        {
            case OperandType.InlineNone:
                return null;
            case OperandType.ShortInlineI:
                return (int)(sbyte)il[offset++];
            case OperandType.InlineI:
                var intValue = BitConverter.ToInt32(il, offset);
                offset += 4;
                return intValue;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                offset += 8;
                return null;
            case OperandType.ShortInlineR:
                offset += 4;
                return null;
            case OperandType.ShortInlineVar:
            case OperandType.ShortInlineBrTarget:
                offset += 1;
                return null;
            case OperandType.InlineVar:
                offset += 2;
                return null;
            case OperandType.InlineBrTarget:
            case OperandType.InlineSig:
            case OperandType.InlineString:
            case OperandType.InlineTok:
            case OperandType.InlineType:
                offset += 4;
                return null;
            case OperandType.InlineField:
                var fieldToken = BitConverter.ToInt32(il, offset);
                offset += 4;
                return module.ResolveField(fieldToken, typeArguments, methodArguments);
            case OperandType.InlineMethod:
                var methodToken = BitConverter.ToInt32(il, offset);
                offset += 4;
                return module.ResolveMethod(methodToken, typeArguments, methodArguments);
            case OperandType.InlineSwitch:
                var count = BitConverter.ToInt32(il, offset);
                offset += 4 + count * 4;
                return null;
            default:
                throw new InvalidOperationException($"Unsupported IL operand type: {operandType}");
        }
    }

    private sealed record RawMoveDetail(MoveDetailIconType Type, Type ModelType, int? Amount);
    private sealed record IlInstruction(int Offset, OpCode OpCode, object? Operand);
}

internal sealed record ResolvedIntentIcon(
    AbstractIntent Intent,
    int OriginalIntentIndex,
    string ImageResourcePath = "",
    MoveDetailIconType MoveDetailType = MoveDetailIconType.None,
    int? Value = null);
