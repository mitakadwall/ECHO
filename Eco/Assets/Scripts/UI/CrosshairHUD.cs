using UnityEngine;
using UnityEngine.UI;

namespace Eco.UI
{
    /// <summary>
    /// Displays a centered aiming reticle while the cursor is locked and highlights
    /// when aiming at a valid Hookable target within grabhook range.
    /// </summary>
    public class CrosshairHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image reticleImage;
        [SerializeField] private Transform aimOrigin;

        [Header("Target Highlight")]
        [SerializeField] private float hookCheckDistance = 28f;
        [SerializeField] private float aimAssistRadius = 0.35f;
        [SerializeField] private string hookableTag = "Hookable";
        [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color hookableColor = new Color(0.2f, 0.95f, 1f, 1f);
        [SerializeField] private float defaultScale = 1f;
        [SerializeField] private float hookableScale = 1.35f;

        private void Awake()
        {
            if (reticleImage == null)
            {
                reticleImage = GetComponentInChildren<Image>();
            }

            if (aimOrigin == null && Camera.main != null)
            {
                aimOrigin = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (reticleImage == null) return;

            bool isAiming = Cursor.lockState == CursorLockMode.Locked;
            if (reticleImage.enabled != isAiming)
            {
                reticleImage.enabled = isAiming;
            }

            if (!isAiming || aimOrigin == null) return;

            bool onHookable = Physics.SphereCast(
                aimOrigin.position,
                aimAssistRadius,
                aimOrigin.forward,
                out RaycastHit hit,
                hookCheckDistance,
                ~0,
                QueryTriggerInteraction.Ignore)
                && !string.IsNullOrEmpty(hookableTag)
                && hit.collider.CompareTag(hookableTag);

            reticleImage.color = onHookable ? hookableColor : defaultColor;
            float targetScale = onHookable ? hookableScale : defaultScale;
            reticleImage.rectTransform.localScale = Vector3.Lerp(
                reticleImage.rectTransform.localScale,
                Vector3.one * targetScale,
                Time.deltaTime * 16f);
        }
    }
}
