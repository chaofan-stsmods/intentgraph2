using IntentGraph2.Models;
using IntentGraph2.Utils.GraphGenerator;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace IntentGraph2.Test;

public class MoveDetailResolverTest : IDisposable
{
    public MoveDetailResolverTest()
    {
        IntentGraphMod.Config.ShowMoveDetail = true;
    }

    public void Dispose()
    {
        IntentGraphMod.Config.ShowMoveDetail = false;
    }

    [Fact]
    public void ResolveIntentIcons_ExpandsMultiplePowersUnderOneDebuffIntent()
    {
        var move = new MoveState("DEBUFF", ApplyTwoDebuffs, new DebuffIntent());

        var icons = MoveDetailResolver.ResolveIntentIcons(move);

        Assert.Equal(2, icons.Count);
        Assert.All(icons, icon => Assert.Equal(MoveDetailIconType.Power, icon.MoveDetailType));
        Assert.All(icons, icon => Assert.Equal(2, icon.Value));
    }

    [Fact]
    public void ResolveIntentIcons_PreservesPowerOrderAcrossDifferentIntentKinds()
    {
        var move = new MoveState(
            "STEAL_STAT",
            StealStat,
            new DebuffIntent(),
            new DefendIntent(),
            new BuffIntent());

        var icons = MoveDetailResolver.ResolveIntentIcons(move);

        Assert.Equal(3, icons.Count);
        Assert.Equal(MoveDetailIconType.Power, icons[0].MoveDetailType);
        Assert.Equal(-2, icons[0].Value);
        Assert.Equal(MoveDetailIconType.None, icons[1].MoveDetailType);
        Assert.Equal(MoveDetailIconType.Power, icons[2].MoveDetailType);
        Assert.Equal(2, icons[2].Value);
    }

    [Fact]
    public void ResolveIntentIcons_UsesSpecificStatusCardAndIntentCount()
    {
        var move = new MoveState("STATUS", AddStatusCards, new StatusIntent(3));

        var icon = Assert.Single(MoveDetailResolver.ResolveIntentIcons(move));

        Assert.Equal(MoveDetailIconType.Status, icon.MoveDetailType);
        Assert.Equal(3, icon.Value);
    }

    [Fact]
    public void ResolveIntentIcons_TracksModelDbPowerPassedToNonGenericApply()
    {
        var move = new MoveState("CARD_DEBUFF_POWER", ApplyNonGenericPower, new CardDebuffIntent());

        var icon = Assert.Single(MoveDetailResolver.ResolveIntentIcons(move));

        Assert.Equal(MoveDetailIconType.Power, icon.MoveDetailType);
        Assert.Equal(1, icon.Value);
    }

    [Fact]
    public void ResolveIntentIcons_EvaluatesPowerAmountFromTargetPropertiesFieldsAndArithmetic()
    {
        var source = new DynamicPowerAmountSource();
        var move = new MoveState("DYNAMIC_POWER_AMOUNT", source.ApplyPower, new BuffIntent());

        var icon = Assert.Single(MoveDetailResolver.ResolveIntentIcons(move));

        Assert.Equal(MoveDetailIconType.Power, icon.MoveDetailType);
        Assert.Equal(6, icon.Value);
    }

    [Fact]
    public void ResolveIntentIcons_EvaluatesNegatedPowerAmountFromTargetProperty()
    {
        var source = new DynamicPowerAmountSource();
        var move = new MoveState("DYNAMIC_NEGATED_POWER_AMOUNT", source.ApplyDebuff, new DebuffIntent());

        var icon = Assert.Single(MoveDetailResolver.ResolveIntentIcons(move));

        Assert.Equal(MoveDetailIconType.Power, icon.MoveDetailType);
        Assert.Equal(-3, icon.Value);
    }

    [Fact]
    public void ResolveIntentIcons_KeepsOriginalIntentWhenOptionIsDisabled()
    {
        IntentGraphMod.Config.ShowMoveDetail = false;
        var move = new MoveState("DEBUFF", ApplyTwoDebuffs, new DebuffIntent());

        var icon = Assert.Single(MoveDetailResolver.ResolveIntentIcons(move));

        Assert.Equal(MoveDetailIconType.None, icon.MoveDetailType);
    }

    private static async Task ApplyTwoDebuffs(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<WeakPower>(null!, targets, 2m, null, null);
        await PowerCmd.Apply<FrailPower>(null!, targets, 2m, null, null);
    }

    private static async Task AddStatusCards(IReadOnlyList<Creature> targets)
    {
        await CardPileCmd.AddToCombatAndPreview<Dazed>(targets, PileType.Discard, 3, null);
    }

    private static async Task StealStat(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(null!, targets, -2m, null, null);
        await PowerCmd.Apply<StrengthPower>(null!, targets, 2m, null, null);
    }

    private static async Task ApplyAffliction(IReadOnlyList<Creature> _)
    {
        await CardCmd.AfflictAndPreview<Entangled>(Array.Empty<CardModel>(), 2m);
    }

    private static async Task ApplyNonGenericPower(IReadOnlyList<Creature> _)
    {
        var power = ModelDb.Power<StrengthPower>().ToMutable();
        await PowerCmd.Apply(null!, power, null!, 1m, null, null);
    }

    private sealed class DynamicPowerAmountSource
    {
        private readonly int _stockAmount = 0;

        private int StrengthGain => 3;

        public async Task ApplyPower(IReadOnlyList<Creature> targets)
        {
            await PowerCmd.Apply<StrengthPower>(null!, targets, StrengthGain * (2 - _stockAmount), null, null);
        }

        public async Task ApplyDebuff(IReadOnlyList<Creature> targets)
        {
            await PowerCmd.Apply<WeakPower>(null!, targets, -StrengthGain, null, null);
        }
    }
}
