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

	public BattleHUD playerHUD;
	public BattleHUD enemyHUD;

	public BattleState state;

	// Set by the UI when the player taps a card button (see OnCardButton).
	private int playerSelectedCard = -1;
	private bool playerHasSelected = false;

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

		dialogueText.text = "A wild " + enemyUnit.unitName + " approaches...";

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

		dialogueText.text = "Choose your attack:";

		// UI should call RefreshHandButtons() (or similar) here to draw
		// buttons for playerUnit.GetHand() - left out of this sketch since
		// it depends on your button prefab setup.
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

		StartCoroutine(ResolveTurn());
	}

	IEnumerator ResolveTurn()
	{
		state = BattleState.RESOLVE;

		// Enemy AI picks now (v0.1: uniformly random from its hand).
		int enemyCard = enemyUnit.ChooseRandomCard();
		int playerCard = playerSelectedCard;

		dialogueText.text = playerUnit.unitName + " plays " + playerCard +
			", " + enemyUnit.unitName + " plays " + enemyCard + "!";
		yield return new WaitForSeconds(1.5f);

		// Same number: total whiff, no damage, both cards already spent.
		if (playerCard == enemyCard)
		{
			dialogueText.text = "Both attacks collide and cancel out!";
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
			dialogueText.text = secondUnit.unitName + " was defeated before it could act!";
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
		dialogueText.text = attacker.unitName + " attacks for " + cardValue + "!";
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
			dialogueText.text = "You won the battle!";
		}
		else
		{
			state = BattleState.LOST;
			dialogueText.text = "You were defeated.";
		}
	}
}
