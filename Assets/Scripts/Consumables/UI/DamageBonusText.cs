using TMPro;
using UnityEngine;

namespace SlidingSiege
{
    /// Drives a TMP text label from a 0..1 value, formatted as a percentage.
    /// Hook DamageBonusSystem.OnFillChanged -> SetFill in the Inspector.
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DamageBonusText : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private string format = "{0:0}%";

        private void Reset() => label = GetComponent<TextMeshProUGUI>();

        public void SetFill(float value)
        {
            if (label == null) label = GetComponent<TextMeshProUGUI>();
            label.text = string.Format(format, Mathf.Clamp01(value) * 100f);
        }
    }
}
