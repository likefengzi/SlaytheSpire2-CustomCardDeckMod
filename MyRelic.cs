using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace CustomCardDeckMod;

public class MyRelic : RelicModel, IPoolModel
{
    public override RelicRarity Rarity { get; } = RelicRarity.Starter;
    public string EnergyColorName { get; } = "colorless";
    public decimal DamageReceivedThisTurn { get; set; }
    public decimal MaxDamageReceivedEveryTurn { get; set; } = 50m;

    //获得遗物之后
    public override async Task AfterObtained()
    {
        //MyUtils.LogInfo(this.Id.Entry);
        await PlayerCmd.GainGold(999, base.Owner, false);

        //await this.AddCards();
    }

    //抽牌前
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player.Creature.CombatState != null && player.Creature.CombatState.RoundNumber > 1)
        {
            return;
        }

        if (player != base.Owner)
        {
            return;
        }

        await this.SelectCardExhaust(this, player, choiceContext, combatState);
        await Task.Delay(1000);
        await this.UpgradeAllCardInCombat(player);
    }

    //战斗胜利后
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (!base.Owner.Creature.IsDead)
        {
            base.Flash();
            await CreatureCmd.Heal(base.Owner.Creature, 9, true);
        }
    }

    //修改奖励
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
            rewards.Add(new GoldReward(0, 99, player));
            rewards.Add(new CardReward(CardCreationOptions.ForRoom(base.Owner, room.RoomType), 3, player));
            rewards.Add(new CardRemovalReward(player));
            return true;
        }

        return false;
    }

    //受伤后
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return Task.CompletedTask;
        }

        if (target != base.Owner.Creature)
        {
            return Task.CompletedTask;
        }

        this.DamageReceivedThisTurn += result.UnblockedDamage;
        return Task.CompletedTask;
    }

    //回合开始前
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        CombatState combatState)
    {
        if (side != CombatSide.Player)
        {
            return Task.CompletedTask;
        }

        this.DamageReceivedThisTurn = 0m;
        return Task.CompletedTask;
    }

    //修改受伤数值
    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return amount;
        }

        if (target != base.Owner.Creature)
        {
            return amount;
        }

        return Math.Min(amount, this.MaxDamageReceivedEveryTurn - this.DamageReceivedThisTurn);
    }

    //获得金币后
    public override async Task AfterGoldGained(Player player)
    {
        //await this.SelectCardEnchantment(this, this.Owner);
    }

    //升级全部卡牌
    public async Task UpgradeAllCardInCombat(Player player)
    {
        foreach (CardModel card in PileType.Draw.GetPile(player).Cards.ToList())
        {
            CardCmd.Upgrade(card);
        }
    }

    //购买删卡后重置价格
    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (itemPurchased is MerchantCardRemovalEntry)
        {
            player.ExtraFields.CardShopRemovalsUsed = 0;
            //merchantCardRemovalEntry.Used = false;
        }

        return base.AfterItemPurchased(player, itemPurchased, goldSpent);
    }

    //从牌库选卡消耗
    public async Task SelectCardExhaust(RelicModel __instance, Player player,
        PlayerChoiceContext choiceContext, CombatState combatState)
    {
        CardModel cardToExhaust = null;

        //List<CardModel> cardsToExhaust = new List<CardModel>();
        //List<CardModel> cardsInDraw = PileType.Draw.GetPile(player).Cards.ToList();

        CardSelectorPrefs prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, 999)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        //IEnumerable<CardModel> source = await CardSelectCmd.FromDeckGeneric(player, prefs);
        IEnumerable<CardModel> source = await CardSelectCmd.FromDeckGeneric(player, prefs, null,
            delegate(CardModel c)
            {
                // 非诅咒卡：按卡牌ID的哈希值排序（确保同名卡相邻）
                if (c.Type != CardType.Curse)
                {
                    return c.Id.GetHashCode();
                }

                // 诅咒卡：排到最后
                return Int32.MaxValue;
            }
        );
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


    //选卡附魔
    public async Task SelectCardEnchantment(RelicModel __instance, Player player)
    {
        EnchantmentModel enchantment = this.GetRandomEnchantment();
        CardSelectorPrefs prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        IEnumerable<CardModel> source = await CardSelectCmd.FromDeckForEnchantment(
            player, enchantment, 1, null, prefs
        );
        foreach (CardModel card in source)
        {
            CardCmd.Enchant(enchantment.ToMutable(), card, 1);
            CardCmd.Preview(card);
        }
    }

    //随机附魔
    public EnchantmentModel GetRandomEnchantment()
    {
        return ModelDb.Enchantment<Sharp>();
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