using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace CustomCardDeckMod;

public class MyCardSelectCmd
{
    public static MegaCrit.Sts2.Core.TestSupport.ICardSelector Selector = CardSelectCmd.Selector;

    public static async Task<IEnumerable<CardModel>> FromDeckGeneric(Player player, CardSelectorPrefs prefs,
        Func<CardModel, bool>? filter = null, Func<CardModel, int>? sortingOrder = null)
    {
        List<CardModel> source = PileType.Draw.GetPile(player).Cards.ToList();
        //List<CardModel> source = PileType.Deck.GetPile(player).Cards.ToList();
        List<CardModel> list = ((filter == null) ? source.ToList() : source.Where(filter).ToList());
        if (player.Creature.IsDead)
        {
            return Array.Empty<CardModel>();
        }

        if (sortingOrder != null)
        {
            list = list.OrderBy(sortingOrder).ToList();
        }

        IEnumerable<CardModel> enumerable;
        if (!prefs.RequireManualConfirmation && list.Count <= prefs.MinSelect)
        {
            enumerable = list;
        }
        else
        {
            uint choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(player);
            if (ShouldSelectLocalCard(player))
            {
                if (Selector != null)
                {
                    enumerable = await Selector.GetSelectedCards(list, prefs.MinSelect, prefs.MaxSelect);
                }
                else
                {
                    NDeckCardSelectScreen nDeckCardSelectScreen = NDeckCardSelectScreen.Create(list, prefs);
                    NOverlayStack.Instance.Push(nDeckCardSelectScreen);
                    enumerable = await nDeckCardSelectScreen.CardsSelected();
                }

                RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(player, choiceId,
                    PlayerChoiceResult.FromMutableDeckCards(enumerable));
            }
            else
            {
                enumerable = (await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(player, choiceId))
                    .AsDeckCards();
            }
        }

        LogChoice(player, enumerable);
        return enumerable;
    }

    private static void LogChoice(Player player, IEnumerable<CardModel?> cards)
    {
        string value = string.Join(",", from c in cards.OfType<CardModel>()
            select c.Id.Entry);
        Log.Info($"Player {player.NetId} chose cards [{value}]");
    }

    private static bool ShouldSelectLocalCard(Player player)
    {
        if (LocalContext.IsMe(player))
        {
            return RunManager.Instance.NetService.Type != NetGameType.Replay;
        }

        return false;
    }
}