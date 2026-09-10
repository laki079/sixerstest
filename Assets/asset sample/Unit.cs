using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
	public string unitName;
	public int unitLevel;

	public int maxHP;
	public int currentHP;

	// The starting deck for this unit, e.g. {6,6,6,5,5,4} for a glass cannon,
	// or {1,1,1,2,2,3} for a swarm-type. Set this in the Inspector per-character.
	public List<int> startingDeck = new List<int> { 1, 2, 3, 4, 5, 6 };

	// The current hand: cards not yet played this cycle.
	private List<int> hand = new List<int>();

	// The card this unit chose for the current turn (set during selection phase).
	public int ChosenCard { get; private set; }

	void Awake()
	{
		ResetHand();
	}

	// Refills the hand back to the full starting deck. Called on Awake and
	// automatically whenever the hand is emptied (instant reshuffle, v0.1 rule).
	public void ResetHand()
	{
		hand = new List<int>(startingDeck);
	}

	public List<int> GetHand()
	{
		return hand;
	}

	public bool HasCard(int number)
	{
		return hand.Contains(number);
	}

	// Player/AI calls this during the selection phase to lock in a choice.
	// The card is removed from the hand immediately (spent on selection,
	// regardless of what happens when it resolves).
	public bool ChooseCard(int number)
	{
		if (!hand.Contains(number))
			return false;

		hand.Remove(number);
		ChosenCard = number;

		// Instant reshuffle: if that was the last card, refill right away.
		if (hand.Count == 0)
			ResetHand();

		return true;
	}

	// Simple random AI pick from whatever's left in hand.
	public int ChooseRandomCard()
	{
		int index = Random.Range(0, hand.Count);
		int chosen = hand[index];
		ChooseCard(chosen);
		return chosen;
	}

	// Damage = the card number itself, flat.
	public bool TakeDamage(int dmg)
	{
		currentHP -= dmg;

		if (currentHP <= 0)
		{
			currentHP = 0;
			return true; // dead
		}
		return false;
	}

	public void Heal(int amount)
	{
		currentHP += amount;
		if (currentHP > maxHP)
			currentHP = maxHP;
	}

	public bool IsDead()
	{
		return currentHP <= 0;
	}
}
