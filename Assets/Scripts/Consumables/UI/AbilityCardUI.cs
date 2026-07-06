using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// One attack/item card. Put this on your card prefab and assign the
    /// pieces in the inspector (icon Image, TMP name/count/damage, Button,
    /// and an optional selection highlight object).
    public class AbilityCardUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private TextMeshProUGUI damageText;   // hidden for items
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedHighlight;

        private Action _onClick;

        private void Awake() => button.onClick.AddListener(() => _onClick?.Invoke());

        public void Setup(Sprite iconSprite, string displayName, int count, string damageLabel,
            bool selected, bool interactable, Action onClick)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            nameText.text = displayName;
            countText.text = "x" + count;
            if (damageText != null)
            {
                damageText.text = damageLabel;
                damageText.gameObject.SetActive(!string.IsNullOrEmpty(damageLabel));
            }
            if (selectedHighlight != null) selectedHighlight.SetActive(selected);
            button.interactable = interactable;
            _onClick = onClick;
        }
    }
}
