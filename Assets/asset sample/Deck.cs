using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a unit's personal deck of attack-number cards (1-6, duplicates allowed).
/// Cards are drawn into a "hand" that shrinks as they're played, then the full
/// deck reshuffles back into the hand once it's empty.
/// </summary>
public class Deck
{
    // The permanent definition of this unit's deck, e.g. {6,6,6,5,5,4}.
    // Never mutated after construction - Hand is reset from this on reshuffle.
    private readonly List<int> deckDefinition;

    // The current playable hand - shrinks as cards are played.
    public List<int> Hand { get; private set; }

    public Deck(List<int> deckDefinition)
    {
        if (deckDefinition == null || deckDefinition.Count != 6)
        {
            Debug.LogError(
                $"Deck must contain exactly 6 cards, but got " +
                $"{(deckDefinition == null ? "null" : deckDefinition.Count.ToString())}. " +
                $"Falling back to a default 1-6 deck.");
            deckDefinition = new List<int> { 1, 2, 3, 4, 5, 6 };
        }

        this.deckDefinition = new List<int>(deckDefinition);
        Reshuffle();
    }

    /// <summary>
    /// Resets the hand back to the full deck definition. Called automatically
    /// once the hand is emptied, per the "instant reshuffle" rule.
    /// </summary>
    public void Reshuffle()
    {
        Hand = new List<int>(deckDefinition);
    }

    /// <summary>
    /// True if the given card value is currently available to play.
    /// </summary>
    public bool CanPlay(int value)
    {
        return Hand.Contains(value);
    }

    /// <summary>
    /// Removes one instance of the given value from the hand. The card is
    /// considered spent the instant this is called, regardless of what
    /// happens to the attack afterward (whiff, kill-prevented, etc).
    /// Reshuffles automatically if this empties the hand.
    /// </summary>
    public void PlayCard(int value)
    {
        if (!CanPlay(value))
        {
            Debug.LogWarning($"Tried to play card {value} but it isn't in hand.");
            return;
        }

        Hand.Remove(value);

        if (Hand.Count == 0)
            Reshuffle();
    }

    /// <summary>
    /// Picks a uniformly random card value from the current hand, without
    /// spending it. Used by enemy AI to decide its move; the caller is
    /// responsible for calling PlayCard() afterward to actually spend it.
    /// </summary>
    public int GetRandomCardValue()
    {
        return Hand[Random.Range(0, Hand.Count)];
    }
}
