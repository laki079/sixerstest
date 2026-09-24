using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleHUD : MonoBehaviour
{
	public Text nameText;
	public Text levelText;
	public Slider hpSlider;
	public Text hpText; // shows "25/25" next to the slider

	// Optional: a parent container where card buttons get spawned for this
	// unit's hand. Only really needed on the player's HUD, but harmless to
	// leave here in case you want to show the enemy's remaining hand size too.
	public Transform handContainer;
	public GameObject cardButtonPrefab; // a simple Button + Text prefab

	// Highlight color used while it's this unit's turn to pick a card.
	// The "off" color is captured automatically from whatever's already
	// on the panel's Image, so you don't need to manually match it.
	public Color activeTurnColor = new Color(1f, 0.85f, 0.4f);

	// Highlight color used while this HUD is a clickable attack target.
	public Color targetableColor = new Color(0.6f, 1f, 0.6f);

	// A Button component on this same HUD panel, used for click-to-target.
	// Assign it in the Inspector on enemy HUDs (party HUDs don't need it).
	public Button selfButton;

	private Image panelBackground;
	private Color normalColor;

	void Awake()
	{
		panelBackground = GetComponent<Image>();
		if (panelBackground != null)
			normalColor = panelBackground.color;
	}

	// Call with true when it's this member's turn to choose, false otherwise.
	public void SetActiveTurn(bool isActive)
	{
		if (panelBackground == null)
			return;

		panelBackground.color = isActive ? activeTurnColor : normalColor;
	}

	// Makes this HUD clickable as an attack target while targetable is true.
	// onSelected fires once when clicked; pass null/false to disable again.
	public void SetTargetable(bool targetable, System.Action onSelected)
	{
		if (panelBackground != null)
			panelBackground.color = targetable ? targetableColor : normalColor;

		if (selfButton == null)
			return;

		selfButton.interactable = targetable;
		selfButton.onClick.RemoveAllListeners();

		if (targetable && onSelected != null)
			selfButton.onClick.AddListener(() => onSelected());
	}

	public void SetHUD(Unit unit)
	{
		nameText.text = unit.unitName;
		levelText.text = "Lvl " + unit.unitLevel;
		hpSlider.maxValue = unit.maxHP;
		hpSlider.value = unit.currentHP;

		if (hpText != null)
			hpText.text = unit.currentHP + "/" + unit.maxHP;
	}

	public void SetHP(int hp)
	{
		hpSlider.value = hp;

		if (hpText != null)
			hpText.text = hp + "/" + (int)hpSlider.maxValue;
	}

	// Spawns one button per card currently in hand. Pass in the callback the
	// BattleSystem wants fired when a card is picked (see OnCardButton).
	public void ShowHand(List<int> hand, System.Action<int> onCardChosen)
	{
		if (handContainer == null || cardButtonPrefab == null)
			return; // hand display not wired up in this HUD - safe no-op

		foreach (Transform child in handContainer)
			Destroy(child.gameObject);

		foreach (int cardValue in hand)
		{
			GameObject buttonGO = Instantiate(cardButtonPrefab, handContainer);
			Text label = buttonGO.GetComponentInChildren<Text>();
			if (label != null)
				label.text = cardValue.ToString();

			Button button = buttonGO.GetComponent<Button>();
			int capturedValue = cardValue; // avoid closure bug in the loop
			button.onClick.AddListener(() => onCardChosen(capturedValue));
		}
	}

	public void ClearHand()
	{
		if (handContainer == null)
			return;

		foreach (Transform child in handContainer)
			Destroy(child.gameObject);
	}
}