using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CustomCardDeckMod;

public class MyRelic : RelicModel, IPoolModel
{
    public override RelicRarity Rarity { get; } = RelicRarity.Starter;
    public string EnergyColorName { get; } = "colorless";

    public override async Task AfterObtained()
    {
        //MyUtils.LogInfo(this.Id.Entry);
        await PlayerCmd.GainGold(999, base.Owner, false);

        //await this.AddCards();
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player.Creature.CombatState != null && player.Creature.CombatState.RoundNumber > 1)
        {
            return;
        }

        await this.SelectCardExhaust(this, player, choiceContext, combatState);
        await Task.Delay(1000);
        await this.UpgradeAllCardInCombat(player);
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (!base.Owner.Creature.IsDead)
        {
            base.Flash();
            await CreatureCmd.Heal(base.Owner.Creature, 9, true);
        }
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != base.Owner)
        {
            return false;
        }

        if (room == null)
        {
            return false;
        }

        if (room.RoomType == RoomType.Monster || room.RoomType == RoomType.Elite || room.RoomType == RoomType.Boss)
        {
            rewards.Add(new CardReward(CardCreationOptions.ForRoom(base.Owner, room.RoomType), 3, player));
            return true;
        }

        return false;
    }

    //升级全部卡牌
    public async Task UpgradeAllCardInCombat(Player player)
    {
        foreach (CardModel card in PileType.Draw.GetPile(player).Cards.ToList())
        {
            CardCmd.Upgrade(card);
        }
    }

    //从牌库选卡消耗
    public async Task SelectCardExhaust(RelicModel __instance, Player player,
        PlayerChoiceContext choiceContext, CombatState combatState)
    {
        CardModel cardToExhaust = null;

        //List<CardModel> cardsToExhaust = new List<CardModel>();
        //List<CardModel> cardsInDraw = PileType.Draw.GetPile(player).Cards.ToList();

        CardSelectorPrefs prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, 999);
        IEnumerable<CardModel> source = await CardSelectCmd.FromDeckGeneric(player, prefs);
        //遍历牌库中选中的卡牌
        foreach (CardModel card in source)
        {
            //如果牌库中选中的卡牌和抽牌堆能对应上，就自定义消耗
            foreach (CardModel cardInDraw in PileType.Draw.GetPile(player).Cards.ToList())
            {
                if (cardInDraw.ToSerializable().Equals(card.ToSerializable()))
                {
                    //cardsToExhaust.Add(cardInDraw);
                    cardToExhaust = cardInDraw;
                    break;
                }
            }

            if (cardToExhaust != null)
            {
                MyUtils.LogInfo(cardToExhaust.ToSerializable().ToString());
                await MyCardCmd.Exhaust(cardToExhaust, false, true);
                //await MyCardCmd.Exhaust(choiceContext, cardToExhaust, false, false);
                cardToExhaust = null;
            }
        }

        // foreach (CardModel card in cardsToExhaust)
        // {
        //     try
        //     {
        //         //await MyCardCmd.Exhaust(choiceContext, card, false, false);
        //         await MyCardCmd.Exhaust(card, false, true);
        //     }
        //     catch (Exception e)
        //     {
        //         var a = e;
        //     }
        // }
    }

    //添加卡牌
    public async Task AddCards()
    {
        List<Reward> list = new List<Reward>();
        List<CardRarity> cardRaritylist = new List<CardRarity>();
        for (int i = 0; i < 5; i++)
        {
            cardRaritylist.Add(CardRarity.Common);
        }

        for (int i = 0; i < 5; i++)
        {
            cardRaritylist.Add(CardRarity.Uncommon);
        }

        for (int i = 0; i < 5; i++)
        {
            cardRaritylist.Add(CardRarity.Rare);
        }

        CardRarity[] array = cardRaritylist.ToArray();
        for (int i = 0; i < array.Length; i++)
        {
            IEnumerable<CardPoolModel> cardPools = new List<CardPoolModel>();
            cardPools.AddItem(base.Owner.Character.CardPool);
            CardRarity rarity = array[i];
            CardCreationOptions options = CardCreationOptions.ForNonCombatWithUniformOdds
                (cardPools, (CardModel c) => c.Rarity == rarity).WithFlags(CardCreationFlags.NoRarityModification);
            list.Add(new CardReward(options, 3, base.Owner));
        }

        await RewardsCmd.OfferCustom(base.Owner, list);
    }
}