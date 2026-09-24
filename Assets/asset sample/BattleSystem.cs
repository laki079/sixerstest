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

	// --- Enemy party setup ---
	// Same pattern as the player side: one entry per enemy, same order.
	public List<GameObject> enemyPrefabs = new List<GameObject>();
	public List<Transform> enemyBattleStations = new List<Transform>();
	public List<BattleHUD> enemyHUDs = new List<BattleHUD>();

	private List<Unit> enemyParty = new List<Unit>();

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

	List<Unit> AlivePartyMembers()
	{
		return playerParty.Where(u => !u.IsDead()).ToList();
	}

	List<Unit> AliveEnemies()
	{
		return enemyParty.Where(u => !u.IsDead()).ToList();
	}

	void Start()
	{
		state = BattleState.START;
		StartCoroutine(SetupBattle());
	}

	IEnumerator SetupBattle()
	{
		playerParty.Clear();
		enemyParty.Clear();

		for (int i = 0; i < playerPrefabs.Count; i++)
		{
			GameObject go = Instantiate(playerPrefabs[i], playerBattleStations[i]);
			Unit unit = go.GetComponent<Unit>();
			playerParty.Add(unit);
			playerHUDs[i].SetHUD(unit);
		}

		List<string> enemyNames = new List<string>();
		for (int i = 0; i < enemyPrefabs.Count; i++)
		{
			GameObject go = Instantiate(enemyPrefabs[i], enemyBattleStations[i]);
			Unit unit = go.GetComponent<Unit>();
			enemyParty.Add(unit);
			enemyHUDs[i].SetHUD(unit);
			enemyNames.Add(unit.unitName);
		}

		string introText = enemyNames.Count == 1
			? "적 파티 " + enemyNames[0] + "이(가) 나타났다..."
			: "적 파티 등장: " + string.Join(", ", enemyNames) + "!";

		dialogueText.text = introText;
		AddLog(introText);

		yield return new WaitForSeconds(2f);

		BeginSelectionPhase();
	}

	void BeginSelectionPhase()
	{
		state = BattleState.SELECTION;
		selectionIndex = 0;
		PromptNextPartyMemberSelection();
	}

	// Walks through the party one at a time: pick a target, then pick a
	// card. Nobody's attack actually happens yet - this just collects
	// everyone's choices before ResolveRound() runs them all together.
	void PromptNextPartyMemberSelection()
	{
		// Skip any party members who are already dead.
		while (selectionIndex < playerParty.Count && playerParty[selectionIndex].IsDead())
			selectionIndex++;

		// Turn off every party highlight first, then light up just the current one.
		for (int i = 0; i < playerHUDs.Count; i++)
			playerHUDs[i].SetActiveTurn(false);

		if (selectionIndex >= playerParty.Count)
		{
			StartCoroutine(ResolveRound());
			return;
		}

		Unit currentMember = playerParty[selectionIndex];
		playerHUDs[selectionIndex].SetActiveTurn(true);

		PromptTargetSelection(currentMember);
	}

	// Step 1 of a party member's turn: click an enemy HUD to target it.
	void PromptTargetSelection(Unit member)
	{
		dialogueText.text = member.unitName + ", 공격할 대상을 선택:";

		for (int i = 0; i < enemyParty.Count; i++)
		{
			Unit enemy = enemyParty[i];
			bool canTarget = !enemy.IsDead();

			if (canTarget)
			{
				enemyHUDs[i].SetTargetable(true, () => OnEnemyTargetChosen(member, enemy));
			}
			else
			{
				enemyHUDs[i].SetTargetable(false, null);
			}
		}
	}

	void OnEnemyTargetChosen(Unit member, Unit target)
	{
		if (state != BattleState.SELECTION)
			return;

		member.ChosenTarget = target;

		// Turn off targeting on every enemy HUD now that a choice was made.
		foreach (BattleHUD hud in enemyHUDs)
			hud.SetTargetable(false, null);

		PromptCardSelection(member);
	}

	// Step 2 of a party member's turn: pick a card from their hand.
	void PromptCardSelection(Unit member)
	{
		dialogueText.text = member.unitName + "이(가) " + member.ChosenTarget.unitName +
			"에세 공격!:";

		int capturedIndex = selectionIndex; // avoid closure bug
		playerHUDs[capturedIndex].ShowHand(member.GetHand(),
			(cardNumber) => OnCardButton(member, capturedIndex, cardNumber));
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
		List<Unit> aliveMembers = AlivePartyMembers();
		int index = Random.Range(0, aliveMembers.Count);
		return aliveMembers[index];
	}

	IEnumerator ResolveRound()
	{
		state = BattleState.RESOLVE;

		List<BattleAction> actions = new List<BattleAction>();

		// Every living party member attacks the target they picked.
		foreach (Unit member in playerParty)
		{
			if (member.IsDead())
				continue;

			actions.Add(new BattleAction
			{
				actor = member,
				target = member.ChosenTarget,
				cardValue = member.ChosenCard
			});
		}

		// Every living enemy attacks its own independently-chosen random target.
		foreach (Unit enemy in enemyParty)
		{
			if (enemy.IsDead())
				continue;

			Unit target = ChooseEnemyTarget();
			int card = enemy.ChooseRandomCard();

			actions.Add(new BattleAction
			{
				actor = enemy,
				target = target,
				cardValue = card
			});
		}

		// Whiff rule: ANY two (or more) actions sharing the same card
		// number all cancel, regardless of who's attacking whom - even
		// two units on the same side who both happened to play the same number.
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
				dialogueText.text = action.actor.unitName + "의 공격이 대상을 찾지 못했습니다!";
				AddLog("(" + action.actor.unitName + "의 공격이 빗나갔습니다 - 대상이 이미 쓰러짐)");
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
					dialogueText.text = "공격이 서로 충돌하여 무효화되었습니다!";
					AddLog("(" + names + "이(가) 모두 " + action.cardValue + "번을 선택하여 공격이 무효화되었습니다)");

					foreach (var a in collidingGroup)
						loggedWhiffGroups.Add(a);

					yield return new WaitForSeconds(1.5f);
				}
				continue;
			}

			yield return ApplyAttack(action.actor, action.target, action.cardValue);

			if (AliveEnemies().Count == 0)
			{
				EndBattle(true);
				yield break;
			}

			if (AlivePartyMembers().Count == 0)
			{
				EndBattle(false);
				yield break;
			}
		}

		BeginSelectionPhase();
	}

	IEnumerator ApplyAttack(Unit attacker, Unit defender, int cardValue)
	{
		dialogueText.text = attacker.unitName + "이(가) " + defender.unitName + "에게 " + cardValue + "의 피해를 입혔습니다!";
		AddLog("(" + attacker.unitName + "이(가) " + defender.unitName +
			"에게 " + cardValue + "의 피해를 입혔습니다)");

		defender.TakeDamage(cardValue);

		int enemyIndex = enemyParty.IndexOf(defender);
		if (enemyIndex >= 0)
		{
			enemyHUDs[enemyIndex].SetHP(defender.currentHP);
		}
		else
		{
			int playerIndex = playerParty.IndexOf(defender);
			if (playerIndex >= 0)
				playerHUDs[playerIndex].SetHP(defender.currentHP);
		}

		yield return new WaitForSeconds(1.5f);
	}

	void EndBattle(bool playerWon)
	{
		if (playerWon)
		{
			state = BattleState.WON;
			dialogueText.text = "전투에서 승리했습니다!";
			AddLog("(모든 적을 물리쳤습니다 - 승리!)");
		}
		else
		{
			state = BattleState.LOST;
			dialogueText.text = "파티가 전멸했습니다.";
			AddLog("(파티가 전멸했습니다 - 패배)");
		}
	}
}