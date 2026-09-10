using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one unit's name, level, and HP. Unlike the original version,
/// this is now meant to be instantiated once PER UNIT (party member or
/// enemy) rather than existing as a single fixed player/enemy pair, since
/// party size is variable per encounter.
/// </summary>
public class BattleHUD : MonoBehaviour
{
    public Text nameText;
    public Text levelText;
    public Slider hpSlider;

    // The unit this HUD instance is tracking. Kept so the HUD can refresh
    // itself (e.g. after damage) without BattleSystem needing to know which
    // HUD maps to which unit - it just tells the unit, and the unit's HUD
    // reference (see Unit.cs change below) handles the rest.
    private Unit trackedUnit;

    public void SetHUD(Unit unit)
    {
        trackedUnit = unit;
        nameText.text = unit.unitName;
        levelText.text = "Lvl " + unit.unitLevel;
        hpSlider.maxValue = unit.maxHP;
        hpSlider.value = unit.currentHP;
    }

    public void Refresh()
    {
        if (trackedUnit == null) return;
        hpSlider.value = trackedUnit.currentHP;

        // Optional: visually grey out / fade the HUD once the unit dies,
        // so defeated party members and enemies are clearly distinguished
        // from living ones during resolution.
        if (!trackedUnit.IsAlive)
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 0.4f;
        }
    }

    // Kept for direct calls if you'd rather push HP updates explicitly
    // instead of calling Refresh() after every hit.
    public void SetHP(int hp)
    {
        hpSlider.value = hp;
    }
}
