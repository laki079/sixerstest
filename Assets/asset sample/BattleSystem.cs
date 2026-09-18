using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum BattleState { START, SELECTION, RESOLVE, WON, LOST }

public class BattleSystem : MonoBehaviour
{
	// --- Player party setup ---
	// One entry per party member. All three lists must be the same length
	// and in the same order (index 0 = first party member, etc.)
	public List<GameObject> playerPrefabs = new List<GameObject>();
	public List<Transform> playerBattleStations = new List<Transform>();
	public List<BattleHUD> playerHUDs = new List<BattleHUD>();

	private List<Unit> playerParty = new List<Unit>();

	// --- Enemy setup (still just 1 for now; see ChooseEnemyTarget/EnemyParty
	// note below for how this would extend to multiple enemies later) ---
	public GameObject enemyPrefab;
	public Transform enemyBattleStation;
	public BattleHUD enemyHUD;

	private Unit enemyUnit;

	public Text dialogueText;

	// Shows a running history of what's happened this battle, e.g.
	// "(The Rock attacked Player for 5)". Assign a Text object in the
	// Inspector; leave it empty if you don't want logging yet.
	public Text battleLogText;
	private List<string> logMessages = new List<string>();
	private const int maxLogLines = 6;

	public BattleState state;

	// Tracks which party member is currently choosing during SELECTION.
	private int selectionIndex = 0;

	// One planned action for this round: who's attacking, who they're
	// aiming at, and which card they committed. Built fresh each round.
	private class BattleAction
	{
		public Unit actor;
		public Unit target;
		public int cardValue;
		public bool whiffed;
	}

	void AddLog(string message)
	{
		logMessages.Add(message);

		if (logMessages.Count > maxLogLines)
			logMessages.RemoveAt(0);

		if (battleLogText != null)
			battleLogText.text = string.Join("\n\n", logMessages);
	}

	void Start()
	{
		state = BattleState.START;
		StartCoroutine(SetupBattle());
	}

	IEnumerator SetupBattle()
	{
		playerParty.Clear();

		for (int i = 0; i < playerPrefabs.Count; i++)
		{
			GameObject go = Instantiate(playerPrefabs[i], playerBattleStations[i]);
			Unit unit = go.GetComponent<Unit>();
			playerParty.Add(unit);
			playerHUDs[i].SetHUD(unit);
		}

		GameObject enemyGO = Instantiate(enemyPrefab, enemyBattleStation);
		enemyUnit = enemyGO.GetComponent<Unit>();
		enemyHUD.SetHUD(enemyUnit);

		dialogueText.text = "A wild " + enemyUnit.unitName + " approaches...";
		AddLog("A wild " + enemyUnit.unitName + " approaches.");

		yield return new WaitForSeconds(2f);

		BeginSelectionPhase();
	}

	void BeginSelectionPhase()
	{
		state = BattleState.SELECTION;
		selectionIndex = 0;
		PromptNextPartyMemberSelection();
	}

	// Walks through the party one at a time asking for a card. Nobody's
	// attack actually happens yet - this just collects everyone's choice
	// (each unit's ChosenCard) before ResolveRound() runs them all together.
	void PromptNextPartyMemberSelection()
	{
		// Skip any party members who are already dead.
		while (selectionIndex < playerParty.Count && playerParty[selectionIndex].IsDead())
			selectionIndex++;

		// Turn off every highlight first, then light up just the current one.
		for (int i = 0; i < playerHUDs.Count; i++)
			playerHUDs[i].SetActiveTurn(false);

		if (selectionIndex >= playerParty.Count)
		{
			StartCoroutine(ResolveRound());
			return;
		}

		Unit currentMember = playerParty[selectionIndex];
		dialogueText.text = currentMember.unitName + ", choose your attack:";

		playerHUDs[selectionIndex].SetActiveTurn(true);

		int capturedIndex = selectionIndex; // avoid closure bug
		playerHUDs[capturedIndex].ShowHand(currentMember.GetHand(),
			(cardNumber) => OnCardButton(currentMember, capturedIndex, cardNumber));
	}

	// Wired up per-party-member via the lambda above.
	public void OnCardButton(Unit member, int hudIndex, int cardNumber)
	{
		if (state != BattleState.SELECTION)
			return;

		if (!member.ChooseCard(cardNumber))
			return; // card wasn't in hand, ignore

		playerHUDs[hudIndex].ClearHand();

		selectionIndex++;
		PromptNextPartyMemberSelection();
	}

	// v0.1 enemy targeting: purely random among living party members.
	// Swap the body of this method later for behavior patterns (e.g.
	// always target lowest HP, always target whoever went fastest, etc.)
	// without touching anything else in ResolveRound().
	Unit ChooseEnemyTarget()
	{
		List<Unit> aliveMembers = playerParty.Where(u => !u.IsDead()).ToList();
		int index = Random.Range(0, aliveMembers.Count);
		return aliveMembers[index];
	}

	IEnumerator ResolveRound()
	{
		state = BattleState.RESOLVE;

		List<BattleAction> actions = new List<BattleAction>();

		// Every living party member attacks the enemy.
		foreach (Unit member in playerParty)
		{
			if (member.IsDead())
				continue;

			actions.Add(new BattleAction
			{
				actor = member,
				target = enemyUnit,
				cardValue = member.ChosenCard
			});
		}

		// Enemy attacks one random living party member.
		Unit enemyTarget = ChooseEnemyTarget();
		int enemyCard = enemyUnit.ChooseRandomCard();
		actions.Add(new BattleAction
		{
			actor = enemyUnit,
			target = enemyTarget,
			cardValue = enemyCard
		});

		// Whiff rule: ANY two (or more) actions sharing the same card
		// number all cancel, regardless of who's attacking whom - even
		// two party members who both happened to play the same number.
		var groupedByValue = actions.GroupBy(a => a.cardValue);
		foreach (var group in groupedByValue)
		{
			if (group.Count() >= 2)
			{
				foreach (var action in group)
					action.whiffed = true;
			}
		}

		// Resolve in ascending card order (lower number = faster).
		List<BattleAction> orderedActions = actions.OrderBy(a => a.cardValue).ToList();

		HashSet<BattleAction> loggedWhiffGroups = new HashSet<BattleAction>();

		foreach (BattleAction action in orderedActions)
		{
			// Actor died earlier this round (faster attack got them first).
			if (action.actor.IsDead())
				continue;

			// Target died earlier this round - attack has nothing to hit.
			if (action.target.IsDead())
			{
				dialogueText.text = action.actor.unitName + "'s attack finds no target!";
				AddLog("(" + action.actor.unitName + "'s attack fizzled - target already down)");
				yield return new WaitForSeconds(1f);
				continue;
			}

			if (action.whiffed)
			{
				// Only log each colliding group once, not once per member in it.
				if (!loggedWhiffGroups.Contains(action))
				{
					var collidingGroup = orderedActions.Where(a => a.cardValue == action.cardValue && a.whiffed);
					string names = string.Join(", ", collidingGroup.Select(a => a.actor.unitName));
					dialogueText.text = "Attacks collide and cancel out!";
					AddLog("(" + names + " all played " + action.cardValue + " - attacks cancelled)");

					foreach (var a in collidingGroup)
						loggedWhiffGroups.Add(a);

					yield return new WaitForSeconds(1.5f);
				}
				continue;
			}

			yield return ApplyAttack(action.actor, action.target, action.cardValue);

			if (enemyUnit.IsDead())
			{
				EndBattle(true);
				yield break;
			}

			if (playerParty.All(u => u.IsDead()))
			{
				EndBattle(false);
				yield break;
			}
		}

		BeginSelectionPhase();
	}

	IEnumerator ApplyAttack(Unit attacker, Unit defender, int cardValue)
	{
		dialogueText.text = attacker.unitName + " attacks " + defender.unitName + " for " + cardValue + "!";
		AddLog("(" + attacker.unitName + " attacked " + defender.unitName +
			" for " + cardValue + ")");

		defender.TakeDamage(cardValue);

		if (defender == enemyUnit)
		{
			enemyHUD.SetHP(enemyUnit.currentHP);
		}
		else
		{
			int index = playerParty.IndexOf(defender);
			if (index >= 0)
				playerHUDs[index].SetHP(defender.currentHP);
		}

		yield return new WaitForSeconds(1.5f);
	}

	void EndBattle(bool playerWon)
	{
		if (playerWon)
		{
			state = BattleState.WON;
			dialogueText.text = "You won the battle!";
			AddLog("(" + enemyUnit.unitName + " was defeated - you win!)");
		}
		else
		{
			state = BattleState.LOST;
			dialogueText.text = "Your party was defeated.";
			AddLog("(Your party was defeated - you lose!)");
		}
	}
}