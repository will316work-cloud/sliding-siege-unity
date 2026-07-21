using UnityEngine;
using UnityEngine.UI;

namespace SlidingSiege
{
    /// Drives a UI Image's fillAmount (Image Type = Filled) from a 0..1
    /// value. Hook DamageBonusSystem.OnFillChanged -> SetFill in the
    /// Inspector.
    [RequireComponent(typeof(Image))]
    public class DamageBonusFillImage : MonoBehaviour
    {
        [SerializeField] private Image fillImage;

        private void Reset() => fillImage = GetComponent<Image>();

        public void SetFill(float value)
        {
            if (fillImage == null) fillImage = GetComponent<Image>();
            fillImage.fillAmount = Mathf.Clamp01(value);
        }
    }
}
