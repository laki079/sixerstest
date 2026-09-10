using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public string unitName;
    public int unitLevel;

    public int maxHP;
    public int currentHP;

    // The unit's permanent card spread, set per-character in the Inspector.
    // e.g. {6,6,6,5,5,4} for a heavy-hitter, {1,1,1,2,2,3} for a swarm type.
    // Unity CAN serialize a plain List<int>, so this shows up as a normal
    // resizable list field on the prefab - no custom editor needed.
    public List<int> startingDeck = new List<int> { 1, 2, 3, 4, 5, 6 };

    // The unit's live, playable deck at runtime - built from startingDeck.
    // Explicitly marked NonSerialized: Deck is a plain C# class (not
    // [Serializable]) and is only ever meant to exist at runtime after
    // InitDeck() has run, so it should never show up in or be saved by
    // the Inspector. This also silences Unity's serialization warning.
    [System.NonSerialized]
    public Deck deck;

    private const int DECK_SIZE = 6;

    /// <summary>
    /// Runs automatically in the Editor whenever startingDeck is edited in
    /// the Inspector. Pads or trims the list so it's always exactly
    /// DECK_SIZE entries - you can't accidentally ship a unit with 5 or 7
    /// cards. Also clamps each value into the valid 1-6 range.
    /// </summary>
    private void OnValidate()
    {
        if (startingDeck == null)
            startingDeck = new List<int>();

        while (startingDeck.Count < DECK_SIZE)
            startingDeck.Add(1);

        while (startingDeck.Count > DECK_SIZE)
            startingDeck.RemoveAt(startingDeck.Count - 1);

        for (int i = 0; i < startingDeck.Count; i++)
            startingDeck[i] = Mathf.Clamp(startingDeck[i], 1, 6);
    }

    public bool IsAlive => currentHP > 0;

    // Reference to this unit's own HUD instance, assigned at spawn time
    // (one BattleHUD per unit now, rather than one fixed HUD per side).
    // Nullable-safe access lets BattleSystem call unit.hud?.Refresh()
    // without needing a separate parallel list of HUDs to keep in sync.
    public BattleHUD hud;

    /// <summary>
    /// Builds this unit's runtime Deck from its Inspector-configured
    /// startingDeck. Call this once per unit, right after it's spawned into
    /// the battle - before it can be selected for card play.
    /// </summary>
    public void InitDeck()
    {
        deck = new Deck(startingDeck);
    }

    public bool TakeDamage(int dmg)
    {
        currentHP -= dmg;

        if (currentHP <= 0)
        {
            currentHP = 0;
            return true;
        }

        return false;
    }

    public void Heal(int amount)
    {
        currentHP += amount;
        if (currentHP > maxHP)
            currentHP = maxHP;
    }
}