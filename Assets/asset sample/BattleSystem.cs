using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum BattleState { START, SELECTION, RESOLUTION, WON, LOST }

/// <summary>
/// One unit's chosen move for this round: the card value played and who it targets.
/// </summary>
public struct CombatAction
{
    public Unit actor;
    public int cardValue;
    public Unit target;

    public CombatAction(Unit actor, int cardValue, Unit target)
    {
        this.actor = actor;
        this.cardValue = cardValue;
        this.target = target;
    }
}

public class BattleSystem : MonoBehaviour
{
    [Header("Party setup")]
    public List<Unit> playerParty = new List<Unit>();
    public List<Unit> enemyParty = new List<Unit>();

    public Text dialogueText;

    public BattleState state;

    // --- Selection-phase bookkeeping ---
    private List<CombatAction> pendingActions = new List<CombatAction>();
    private int playerSelectIndex;      // which living party member is currently choosing
    private int pendingCardValue = -1;  // card the current party member has picked, awaiting a target

    void Start()
    {
        state = BattleState.START;
        StartCoroutine(SetupBattle());
    }

    IEnumerator SetupBattle()
    {
        // Units are already placed in the scene and assigned to playerParty/
        // enemyParty via the Inspector - we just need to build each one's
        // runtime deck from its startingDeck before anyone can select cards.
        foreach (Unit unit in playerParty.Concat(enemyParty))
        {
            unit.InitDeck();
            unit.hud?.SetHUD(unit);
        }

        dialogueText.text = "The battle begins!";

        yield return new WaitForSeconds(1.5f);

        BeginSelectionPhase();
    }

    // ---------------------------------------------------------
    // SELECTION PHASE
    // ---------------------------------------------------------

    void BeginSelectionPhase()
    {
        state = BattleState.SELECTION;
        pendingActions.Clear();
        pendingCardValue = -1;

        // Enemies choose instantly: random card from their own hand, random
        // living player-party target. Swap this out later for smarter/
        // personality-based AI without touching the rest of the flow.
        foreach (Unit enemy in enemyParty.Where(e => e.IsAlive))
        {
            int card = enemy.deck.GetRandomCardValue();
            enemy.deck.PlayCard(card);

            Unit target = GetRandomLivingTarget(playerParty);
            if (target != null)
                pendingActions.Add(new CombatAction(enemy, card, target));
        }

        playerSelectIndex = 0;
        PromptNextPlayerSelection();
    }

    void PromptNextPlayerSelection()
    {
        // Skip any party members who are already dead.
        while (playerSelectIndex < playerParty.Count && !playerParty[playerSelectIndex].IsAlive)
            playerSelectIndex++;

        if (playerSelectIndex >= playerParty.Count)
        {
            // Everyone on the player side has chosen - move to resolution.
            StartCoroutine(ResolveRound());
            return;
        }

        Unit current = playerParty[playerSelectIndex];
        dialogueText.text = $"{current.unitName}, choose your attack!";

        // UI hook point: display current.deck.Hand as the available card
        // buttons for this unit. Each button's onClick should call
        // SelectCard(value) below.
    }

    /// <summary>
    /// Call this from a card-value button (wired to the current party
    /// member's hand). Stores the choice and waits for a target selection.
    /// </summary>
    public void SelectCard(int cardValue)
    {
        if (state != BattleState.SELECTION) return;

        Unit current = playerParty[playerSelectIndex];
        if (!current.deck.CanPlay(cardValue))
        {
            Debug.LogWarning($"{current.unitName} doesn't have card {cardValue} in hand.");
            return;
        }

        pendingCardValue = cardValue;
        dialogueText.text = $"{current.unitName}, choose a target!";

        // UI hook point: highlight enemyParty as selectable targets. Each
        // target's click/button should call SelectTarget(unit) below.
    }

    /// <summary>
    /// Call this after SelectCard, once the player has clicked/tapped a
    /// target unit. Finalizes this party member's action and moves on.
    /// </summary>
    public void SelectTarget(Unit target)
    {
        if (state != BattleState.SELECTION || pendingCardValue == -1) return;
        if (target == null || !target.IsAlive) return;

        Unit current = playerParty[playerSelectIndex];

        // Card is spent the instant it's chosen, regardless of outcome.
        current.deck.PlayCard(pendingCardValue);
        pendingActions.Add(new CombatAction(current, pendingCardValue, target));

        pendingCardValue = -1;
        playerSelectIndex++;
        PromptNextPlayerSelection();
    }

    Unit GetRandomLivingTarget(List<Unit> party)
    {
        List<Unit> living = party.Where(u => u.IsAlive).ToList();
        if (living.Count == 0) return null;
        return living[Random.Range(0, living.Count)];
    }

    // ---------------------------------------------------------
    // RESOLUTION PHASE
    // ---------------------------------------------------------

    IEnumerator ResolveRound()
    {
        state = BattleState.RESOLUTION;

        // Group all chosen actions by card value. Any value picked by 2+
        // units (any side, any target) whiffs entirely - target-agnostic.
        var groupedByValue = pendingActions.GroupBy(a => a.cardValue);

        List<CombatAction> resolvingActions = new List<CombatAction>();

        foreach (var group in groupedByValue)
        {
            if (group.Count() >= 2)
            {
                dialogueText.text = $"Attacks of power {group.Key} collide and whiff!";
                yield return new WaitForSeconds(1f);
                // No damage - cards already spent, nothing further happens.
            }
            else
            {
                resolvingActions.Add(group.First());
            }
        }

        // Global sort: lowest card value resolves first, across both sides.
        resolvingActions = resolvingActions.OrderBy(a => a.cardValue).ToList();

        foreach (CombatAction action in resolvingActions)
        {
            // The actor may have died earlier this round (killed by a
            // faster attack) - their action never happens.
            if (!action.actor.IsAlive)
                continue;

            // The target may also already be dead from an earlier, faster
            // action this round - nothing to hit.
            if (!action.target.IsAlive)
                continue;

            dialogueText.text = $"{action.actor.unitName} uses {action.cardValue}!";
            yield return new WaitForSeconds(0.75f);

            bool died = action.target.TakeDamage(action.cardValue);
            action.target.hud?.Refresh();

            if (died)
            {
                dialogueText.text = $"{action.target.unitName} was defeated!";
                yield return new WaitForSeconds(1f);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        CheckBattleEnd();
    }

    void CheckBattleEnd()
    {
        bool playerWiped = playerParty.All(u => !u.IsAlive);
        bool enemyWiped = enemyParty.All(u => !u.IsAlive);

        if (enemyWiped)
        {
            state = BattleState.WON;
            dialogueText.text = "You won the battle!";
        }
        else if (playerWiped)
        {
            state = BattleState.LOST;
            dialogueText.text = "Your party was defeated.";
        }
        else
        {
            // Neither side is wiped - loop back to the next round.
            playerSelectIndex = 0;
            BeginSelectionPhase();
        }
    }
}
