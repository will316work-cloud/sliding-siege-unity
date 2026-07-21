using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Drives a UI Slider's value from a 0..1 value. Hook
    /// DamageBonusSystem.OnFillChanged -> SetFill in the Inspector.
    [RequireComponent(typeof(Slider))]
    public class DamageBonusSlider : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        private void Reset() => slider = GetComponent<Slider>();

        public void SetFill(float value)
        {
            if (slider == null) slider = GetComponent<Slider>();
            slider.value = Mathf.Clamp01(value);
        }
    }
}
