using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;

namespace CustomCardDeckMod;

public class MyCardPileCmd
{
    public static async Task<IReadOnlyList<CardPileAddResult>> Add(IEnumerable<CardModel> cards, CardPile newPile, CardPilePosition position = CardPilePosition.Bottom, AbstractModel? source = null, bool skipVisuals = false)
	{
		if (!cards.Any())
		{
			return Array.Empty<CardPileAddResult>();
		}
		if (newPile.IsCombatPile && CombatManager.Instance.IsEnding)
		{
			return cards.Select((CardModel c) => new CardPileAddResult
			{
				cardAdded = c,
				success = false
			}).ToList();
		}
		List<CardPileAddResult> results = new List<CardPileAddResult>();
		Player owningPlayer = null;
		foreach (CardModel card5 in cards)
		{
			if (card5.Owner == null)
			{
				throw new InvalidOperationException(card5.Id.Entry + " has no owner.");
			}
			if (card5.Owner.Creature.IsDead)
			{
				CardPileAddResult item = new CardPileAddResult
				{
					success = false,
					cardAdded = card5,
					oldPile = card5.Pile,
					modifyingModels = null
				};
				results.Add(item);
				continue;
			}
			if (card5.HasBeenRemovedFromState)
			{
				throw new InvalidOperationException(card5.Id.Entry + " has already been removed from its containing state. If this is intentional, make sure to add it back to the state before adding it to a pile.");
			}
			if (newPile.Type == PileType.Deck)
			{
				if (!card5.Owner.RunState.ContainsCard(card5))
				{
					if (card5.Owner.RunState is NullRunState)
					{
						throw new InvalidOperationException("Tried to add card " + card5.Id.Entry + " to deck for an owner with a NullRunState!");
					}
					throw new InvalidOperationException(card5.Id.Entry + " must be added to a RunState before adding it to your deck.");
				}
			}
			else
			{
				CombatState combatState = card5.Owner.Creature.CombatState;
				if (combatState == null || !combatState.ContainsCard(card5))
				{
					throw new InvalidOperationException(card5.Id.Entry + " must be added to a CombatState before adding it to this pile.");
				}
			}
			if (card5.UpgradePreviewType.IsPreview())
			{
				throw new InvalidOperationException("A card preview cannot be added to a pile.");
			}
			CardPileAddResult item2 = new CardPileAddResult
			{
				success = true,
				cardAdded = card5,
				oldPile = card5.Pile,
				modifyingModels = null
			};
			results.Add(item2);
			if (owningPlayer == null)
			{
				owningPlayer = card5.Owner;
			}
			if (owningPlayer == card5.Owner)
			{
				continue;
			}
			throw new InvalidOperationException("Tried to add cards with different owners to the same pile!");
		}
		bool owningPlayerIsLocal = LocalContext.IsMe(owningPlayer);
		if (newPile.Type == PileType.Deck)
		{
			for (int i = 0; i < results.Count; i++)
			{
				CardPileAddResult result = results[i];
				if (Hook.ShouldAddToDeck(owningPlayer.RunState, result.cardAdded, out AbstractModel preventer))
				{
					IRunState runState = owningPlayer.RunState;
					runState.CurrentMapPointHistoryEntry?.GetEntry(owningPlayer.NetId).CardsGained.Add(result.cardAdded.ToSerializable());
					result.cardAdded.FloorAddedToDeck = runState.TotalFloor;
				}
				else
				{
					await preventer.AfterAddToDeckPrevented(result.cardAdded);
					result.success = false;
					results[i] = result;
				}
			}
		}
		if (newPile.IsCombatPile && !CombatManager.Instance.IsInProgress)
		{
			return results;
		}
		if (!results.Any((CardPileAddResult r) => r.success))
		{
			return results;
		}
		List<NCard> cardNodes = new List<NCard>();
		List<CardModel> cardsWithoutNodesChangingPiles = new List<CardModel>();
		for (int i = 0; i < results.Count; i++)
		{
			CardPileAddResult value = results[i];
			if (!value.success)
			{
				continue;
			}
			NCard cardNode = null;
			CardPile oldPile = value.oldPile;
			CardModel card = value.cardAdded;
			CardPile targetPile = newPile;
			int num;
			if (targetPile != null && targetPile.Type == PileType.Hand)
			{
				IReadOnlyList<CardModel> cards2 = targetPile.Cards;
				if (cards2 != null)
				{
					num = ((cards2.Count >= 10) ? 1 : 0);
					goto IL_0535;
				}
			}
			num = 0;
			goto IL_0535;
			IL_0535:
			bool isFullHandAdd = (byte)num != 0;
			if (isFullHandAdd)
			{
				targetPile = CardPile.Get(PileType.Discard, card.Owner);
			}
			int num2;
			if (!owningPlayerIsLocal && targetPile.Type != PileType.Play)
			{
				num2 = ((oldPile != null && oldPile.Type == PileType.Play) ? 1 : 0);
			}
			else
			{
				num2 = 1;
			}
			bool flag = (byte)num2 != 0;
			bool flag2;
			bool flag4;
			bool flag5;
			if (TestMode.IsOff && flag && !skipVisuals)
			{
				cardNode = NCard.FindOnTable(card);
				flag2 = cardNode == null && targetPile.Type.IsCombatPile() && (isFullHandAdd || oldPile != null || targetPile.Type == PileType.Hand);
				bool flag3 = cardNode == null;
				flag4 = flag3;
				if (flag4)
				{
					if (oldPile == null)
					{
						goto IL_064c;
					}
					switch (oldPile.Type)
					{
					case PileType.Draw:
					case PileType.Discard:
					case PileType.Exhaust:
					case PileType.Deck:
						break;
					default:
						goto IL_064c;
					}
					flag5 = true;
					goto IL_064f;
				}
				goto IL_0653;
			}
			goto IL_06dd;
			IL_064f:
			flag4 = flag5;
			goto IL_0653;
			IL_06dd:
			CardModel card2 = card;
			if (oldPile != null)
			{
				card.RemoveFromCurrentPile();
			}
			else if (targetPile.Type == PileType.Deck)
			{
				List<AbstractModel> modifyingModels;
				CardModel cardModel = Hook.ModifyCardBeingAddedToDeck(card.Owner.RunState, card, out modifyingModels);
				card2 = cardModel;
				if (modifyingModels != null && modifyingModels.Count > 0)
				{
					value.cardAdded = cardModel;
					value.modifyingModels = modifyingModels;
					results[i] = value;
				}
			}
			targetPile.AddInternal(card2, position switch
			{
				CardPilePosition.Bottom => -1, 
				CardPilePosition.Top => 0, 
				CardPilePosition.Random => card.Owner.RunState.Rng.Shuffle.NextInt(targetPile.Cards.Count + 1), 
				_ => throw new ArgumentOutOfRangeException("position", position, null), 
			});
			if (oldPile == null && targetPile.IsCombatPile)
			{
				await Hook.AfterCardEnteredCombat(card.CombatState, card);
			}
			if (isFullHandAdd && owningPlayerIsLocal)
			{
				ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), owningPlayer.Creature, 2.0);
			}
			if (oldPile == null || oldPile.Type != PileType.Play || newPile.Type == PileType.Hand || card.IsDupe)
			{
				cardNode?.UpdateVisuals(targetPile.Type, CardPreviewMode.Normal);
			}
			continue;
			IL_0653:
			bool flag6 = flag4;
			if (flag6)
			{
				PileType type = targetPile.Type;
				flag5 = ((type == PileType.Draw || type == PileType.Discard || type == PileType.Deck) ? true : false);
				flag6 = flag5;
			}
			if (flag6)
			{
				cardsWithoutNodesChangingPiles.Add(card);
			}
			else if (flag2)
			{
				cardNode = CreateCardNodeAndUpdateVisuals(card, targetPile.Type, owningPlayerIsLocal);
			}
			if (cardNode != null)
			{
				cardNodes.Add(cardNode);
			}
			goto IL_06dd;
			IL_064c:
			flag5 = false;
			goto IL_064f;
		}
		Tween tween = null;
		if (cardNodes.Count != 0)
		{
			NPlayerHand handNode = NCombatRoom.Instance.Ui.Hand;
			_ = NCombatRoom.Instance.Ui.PlayQueue;
			_ = NCombatRoom.Instance.Ui.PlayContainer;
			tween = NCombatRoom.Instance.CreateTween().SetParallel();
			foreach (NCard cardNode2 in cardNodes)
			{
				CardModel card3 = cardNode2.Model;
				CardPile oldPile2 = results.Find((CardPileAddResult r) => r.cardAdded == card3).oldPile;
				MoveCardNodeToNewPileBeforeTween(cardNode2, card3.Pile.Type);
				bool flag7 = !owningPlayerIsLocal;
				bool flag8 = flag7;
				if (flag8)
				{
					PileType type = card3.Pile.Type;
					bool flag5 = (((uint)(type - 1) <= 2u || type == PileType.Deck) ? true : false);
					flag8 = flag5;
				}
				if (flag8)
				{
					tween.Parallel().TweenProperty(cardNode2, "position", cardNode2.Position + Vector2.Down * 25f, (SaveManager.Instance.PrefsSave.FastMode == FastModeType.Fast) ? 0.2f : 0.3f);
					tween.Parallel().TweenProperty(cardNode2, "modulate", StsColors.exhaustGray, (SaveManager.Instance.PrefsSave.FastMode == FastModeType.Fast) ? 0.2f : 0.3f);
					tween.Chain().TweenCallback(Callable.From(cardNode2.QueueFreeSafely));
					continue;
				}
				switch (card3.Pile.Type)
				{
				case PileType.Exhaust:
					card3.Pile.InvokeCardAddFinished();
					if (oldPile2 != null && oldPile2.Type != PileType.Hand && oldPile2.Type != PileType.Play)
					{
						AppendPileLerpTween(tween, cardNode2, PileType.Play, oldPile2);
						FastModeType fastMode = SaveManager.Instance.PrefsSave.FastMode;
						tween.Chain().TweenInterval(fastMode switch
						{
							FastModeType.Instant => 0.01f, 
							FastModeType.Fast => 0.2f, 
							_ => 0.5f, 
						});
					}
					tween.Chain().TweenCallback(Callable.From(delegate
					{
						NCombatRoom.Instance.Ui.AddChildSafely(NExhaustVfx.Create(cardNode2));
					}));
					tween.Parallel().TweenProperty(cardNode2, "modulate", StsColors.exhaustGray, (SaveManager.Instance.PrefsSave.FastMode == FastModeType.Fast) ? 0.2f : 0.3f);
					tween.Chain().TweenCallback(Callable.From(cardNode2.QueueFreeSafely));
					break;
				case PileType.Hand:
					AppendPileLerpTween(tween, cardNode2, card3.Pile.Type, oldPile2);
					tween.Parallel().TweenCallback(Callable.From(delegate
					{
						handNode.Add(cardNode2);
					}));
					break;
				case PileType.Play:
					AppendPlayPileLerpTween(tween, cardNode2, oldPile2);
					break;
				default:
					tween.TweenCallback(Callable.From(delegate
					{
						Node node = ((card3.Pile.Type != PileType.Deck) ? NCombatRoom.Instance.CombatVfxContainer : NRun.Instance.GlobalUi.TopBar.TrailContainer);
						cardNode2.Reparent(node);
						Vector2 targetPosition = card3.Pile.Type.GetTargetPosition(cardNode2);
						NCardFlyVfx child2 = NCardFlyVfx.Create(cardNode2, targetPosition, isAddingToPile: true, card3.Owner.Character.TrailPath);
						node.AddChildSafely(child2);
					}));
					break;
				}
			}
		}
		if (cardsWithoutNodesChangingPiles.Count != 0)
		{
			foreach (CardModel card4 in cardsWithoutNodesChangingPiles)
			{
				CardPile oldPile3 = results.Find((CardPileAddResult r) => r.cardAdded == card4).oldPile;
				Node vfxContainer = ((card4.Pile.Type != PileType.Deck) ? NCombatRoom.Instance.CombatVfxContainer : NRun.Instance.GlobalUi.TopBar.TrailContainer);
				if (tween != null)
				{
					tween.TweenCallback(Callable.From(delegate
					{
						NCardFlyShuffleVfx child2 = NCardFlyShuffleVfx.Create(oldPile3, card4.Pile, card4.Owner.Character.TrailPath);
						vfxContainer.AddChildSafely(child2);
					}));
				}
				else
				{
					NCardFlyShuffleVfx child = NCardFlyShuffleVfx.Create(oldPile3, card4.Pile, card4.Owner.Character.TrailPath);
					vfxContainer.AddChildSafely(child);
				}
			}
		}
		if (tween != null)
		{
			tween.Play();
			if (tween.IsValid() && tween.IsRunning())
			{
				await NCombatRoom.Instance.ToSignal(tween, Tween.SignalName.Finished);
			}
		}
		foreach (CardPileAddResult item3 in results)
		{
			if (item3.success)
			{
				CardModel cardAdded = item3.cardAdded;
				await Hook.AfterCardChangedPiles(cardAdded.Owner.RunState, cardAdded.CombatState, cardAdded, item3.oldPile?.Type ?? PileType.None, source);
			}
		}
		return results;
	}
    
	private static NCard CreateCardNodeAndUpdateVisuals(CardModel card, PileType targetPileType, bool owningPlayerIsLocal)
	{
		NCard nCard = NCard.Create(card);
		NCombatRoom.Instance.Ui.AddChildSafely(nCard);
		nCard.UpdateVisuals(targetPileType, CardPreviewMode.Normal);
		if (!owningPlayerIsLocal)
		{
			nCard.Position = NCombatRoom.Instance.GetCreatureNode(card.Owner.Creature).IntentContainer.GlobalPosition;
		}
		else if (card.Pile != null)
		{
			nCard.Position = card.Pile.Type.GetTargetPosition(nCard);
		}
		else
		{
			nCard.Position = targetPileType.GetTargetPosition(nCard);
		}
		return nCard;
	}
	
	private static void MoveCardNodeToNewPileBeforeTween(NCard cardNode, PileType newPileType)
	{
		NPlayerHand hand = NCombatRoom.Instance.Ui.Hand;
		NCardPlayQueue playQueue = NCombatRoom.Instance.Ui.PlayQueue;
		Control playContainer = NCombatRoom.Instance.Ui.PlayContainer;
		Vector2 globalPosition = cardNode.GlobalPosition;
		CardModel model = cardNode.Model;
		if (playQueue.IsAncestorOf(cardNode))
		{
			playQueue.RemoveCardFromQueueForExecution(model);
		}
		if (hand.IsAncestorOf(cardNode))
		{
			hand.Remove(model);
		}
		else
		{
			cardNode.GetParent()?.RemoveChildSafely(cardNode);
		}
		if (newPileType == PileType.Play)
		{
			playContainer.AddChildSafely(cardNode);
			if (NCombatUi.IsDebugHidingPlayContainer)
			{
				cardNode.Visible = false;
			}
		}
		else
		{
			NCombatRoom.Instance.Ui.AddChildSafely(cardNode);
		}
		cardNode.GlobalPosition = globalPosition;
		cardNode.PlayPileTween?.FastForwardToCompletion();
	}
	
	private static void AppendPileLerpTween(Tween tween, NCard cardNode, PileType typePile, CardPile? oldPile)
	{
		Vector2 targetPosition = typePile.GetTargetPosition(cardNode);
		float num = SaveManager.Instance.PrefsSave.FastMode switch
		{
			FastModeType.Instant => 0.01f, 
			FastModeType.Fast => 0.1f, 
			_ => 0.25f, 
		};
		if (typePile != PileType.Hand)
		{
			tween.TweenProperty(cardNode, "position", targetPosition, num).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		}
		if (typePile == PileType.Play)
		{
			tween.TweenProperty(cardNode, "scale", Vector2.One * 0.8f, 0.25).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		}
		else if (oldPile == null)
		{
			tween.TweenProperty(cardNode, "scale", Vector2.One, num).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic)
				.From(Vector2.Zero);
		}
		else
		{
			tween.Parallel().TweenProperty(cardNode, "scale", Vector2.One, num).SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Cubic);
		}
	}
	
	private static void AppendPlayPileLerpTween(Tween tween, NCard cardNode, CardPile? oldPile)
	{
		AppendPileLerpTween(tween, cardNode, cardNode.Model.Pile.Type, oldPile);
		tween.Parallel().TweenCallback(Callable.From(delegate
		{
			NCombatRoom.Instance.Ui.AddToPlayContainer(cardNode);
		}));
	}
}