using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace CustomCardDeckMod
{
    public class MyCardCmd
    {
        public static async Task Exhaust(CardModel card,
            bool causedByEthereal = false, bool skipVisuals = false)
        {
            if (!CombatManager.Instance.IsOverOrEnding)
            {
                CombatState combatState = card.CombatState ?? card.Owner.Creature.CombatState;
                await CardPileCmd.Add(card, PileType.Exhaust, CardPilePosition.Bottom, null, skipVisuals);
                CombatManager.Instance.History.CardExhausted(combatState, card);
                //await Hook.AfterCardExhausted(combatState, choiceContext, card, causedByEthereal);
            }
        }

        // public static async Task Exhaust(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal = false, bool skipVisuals = false)
        // {
        //     if (!CombatManager.Instance.IsOverOrEnding)
        //     {
        //         CombatState combatState = card.CombatState ?? card.Owner.Creature.CombatState;
        //         await CardPileCmd.Add(card, PileType.Exhaust, CardPilePosition.Bottom, null, skipVisuals);
        //         CombatManager.Instance.History.CardExhausted(combatState, card);
        //         await Hook.AfterCardExhausted(combatState, choiceContext, card, causedByEthereal);
        //     }
        // }
    }
}