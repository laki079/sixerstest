using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BattleState { START, SELECTION, RESOLVE, WON, LOST }

public class BattleSystem : MonoBehaviour
{
	public GameObject playerPrefab;
	public GameObject enemyPrefab;

	public Transform playerBattleStation;
	public Transform enemyBattleStation;

	Unit playerUnit;
	Unit enemyUnit;

	public Text dialogueText;

	// Shows a running history of what's happened this battle, e.g.
	// "(The Rock attacked Player for 5)". Assign a Text object in the
	// Inspector; leave it empty if you don't want logging yet.
	public Text battleLogText;
	private List<string> logMessages = new List<string>();
	private const int maxLogLines = 6;

	public BattleHUD playerHUD;
	public BattleHUD enemyHUD;

	public BattleState state;

	// Set by the UI when the player taps a card button (see OnCardButton).
	private int playerSelectedCard = -1;
	private bool playerHasSelected = false;

	// Appends a line to the battle log, dropping the oldest line once
	// maxLogLines is exceeded so the text box doesn't grow forever.
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
		GameObject playerGO = Instantiate(playerPrefab, playerBattleStation);
		playerUnit = playerGO.GetComponent<Unit>();

		GameObject enemyGO = Instantiate(enemyPrefab, enemyBattleStation);
		enemyUnit = enemyGO.GetComponent<Unit>();

		dialogueText.text = enemyUnit.unitName + " 의/가 등장!";
		AddLog( enemyUnit.unitName + " 등장!");

		playerHUD.SetHUD(playerUnit);
		enemyHUD.SetHUD(enemyUnit);

		yield return new WaitForSeconds(2f);

		BeginSelectionPhase();
	}

	void BeginSelectionPhase()
	{
		state = BattleState.SELECTION;
		playerHasSelected = false;
		playerSelectedCard = -1;

		dialogueText.text = " 공격 선택:";

		playerHUD.ShowHand(playerUnit.GetHand(), OnCardButton);
	}

	// Wire this up to each of the 6 card buttons, passing the card's number.
	public void OnCardButton(int cardNumber)
	{
		if (state != BattleState.SELECTION || playerHasSelected)
			return;

		if (!playerUnit.ChooseCard(cardNumber))
			return; // card wasn't in hand, ignore

		playerSelectedCard = cardNumber;
		playerHasSelected = true;

		playerHUD.ClearHand();

		StartCoroutine(ResolveTurn());
	}

	IEnumerator ResolveTurn()
	{
		state = BattleState.RESOLVE;

		// Enemy AI picks now (v0.1: uniformly random from its hand).
		int enemyCard = enemyUnit.ChooseRandomCard();
		int playerCard = playerSelectedCard;

		dialogueText.text = playerUnit.unitName  + playerCard+ " 의 공격!   " + enemyUnit.unitName + " 는 " + enemyCard + " 의 공격!";
		yield return new WaitForSeconds(1.5f);

		// Same number: total whiff, no damage, both cards already spent.
		if (playerCard == enemyCard)
		{
			dialogueText.text = " 쌍방 공격으로 무효화!";
			AddLog("(" + playerUnit.unitName + " 와 " + enemyUnit.unitName +
				" 양측의 " + playerCard + " 공격은 쌍방 공격으로 무효화!)");
			yield return new WaitForSeconds(1.5f);
			BeginSelectionPhase();
			yield break;
		}

		// Lower number = faster = resolves first.
		Unit firstUnit = playerCard < enemyCard ? playerUnit : enemyUnit;
		Unit secondUnit = playerCard < enemyCard ? enemyUnit : playerUnit;
		int firstCard = Mathf.Min(playerCard, enemyCard);
		int secondCard = Mathf.Max(playerCard, enemyCard);

		// First (faster) attack resolves.
		yield return ApplyAttack(firstUnit, secondUnit, firstCard);

		if (secondUnit.IsDead())
		{
			dialogueText.text = secondUnit.unitName + " 가 행동전에 사망!";
			AddLog("(" + secondUnit.unitName + " 가 행동전에 사망!)");
			yield return new WaitForSeconds(1.5f);
			EndBattle(secondUnit);
			yield break;
		}

		yield return new WaitForSeconds(1f);

		// Second (slower) attack resolves normally, since the faster one didn't kill.
		yield return ApplyAttack(secondUnit, firstUnit, secondCard);

		if (firstUnit.IsDead())
		{
			EndBattle(firstUnit);
			yield break;
		}

		BeginSelectionPhase();
	}

	IEnumerator ApplyAttack(Unit attacker, Unit defender, int cardValue)
	{
		dialogueText.text = attacker.unitName + " 의 공격" + cardValue + "! ";
		AddLog("(" + attacker.unitName +" 가" + defender.unitName+ "에개 " +cardValue + " 만큼 공격!  )");

		defender.TakeDamage(cardValue);

		if (defender == playerUnit)
			playerHUD.SetHP(playerUnit.currentHP);
		else
			enemyHUD.SetHP(enemyUnit.currentHP);

		yield return new WaitForSeconds(1.5f);
	}

	void EndBattle(Unit defeatedUnit)
	{
		if (defeatedUnit == enemyUnit)
		{
			state = BattleState.WON;
			dialogueText.text = "승리!";
			AddLog("(" + enemyUnit.unitName + " 패배 - 승리했습니다!)");
		}
		else
		{
			state = BattleState.LOST;
			dialogueText.text = "패배";
			AddLog("(" + playerUnit.unitName + " 가 승리 - 패배했다...)");
		}
	}
}