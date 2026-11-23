using UnityEngine;
using UnityEngine.InputSystem;
using Eco.Interaction;

namespace Eco.Player
{
    /// <summary>
    /// Unified E-key interaction system for EquipmentPickups, Puzzle Monitors, Puzzle Levers,
    /// and any other IInteractable in the world.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private Transform viewOrigin;
        [SerializeField] private float interactRange = 3.2f;
        [SerializeField] private Key interactKey = Key.E;

        private IInteractable currentTarget;
        private GUIStyle promptStyle;

        private void Awake()
        {
            if (viewOrigin == null && Camera.main != null)
            {
                viewOrigin = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (viewOrigin == null && Camera.main != null)
            {
                viewOrigin = Camera.main.transform;
            }

            currentTarget = FindBestInteractable();

            if (currentTarget != null && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                currentTarget.Interact(gameObject);
                currentTarget = FindBestInteractable();
            }
        }

        private IInteractable FindBestInteractable()
        {
            Transform origin = viewOrigin != null ? viewOrigin : transform;

            // 1. Prefer direct look raycast within interaction range (gives exact lever/monitor selection)
            if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Collide))
            {
                IInteractable looked = hit.collider.GetComponentInParent<IInteractable>();
                if (looked != null && looked.CanInteract(gameObject))
                {
                    return looked;
                }
            }

            // 2. Fallback to closest/most-centered available interactable within proximity radius
            Collider[] nearby = Physics.OverlapSphere(origin.position, interactRange, ~0, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestScore = float.MaxValue;

            foreach (Collider col in nearby)
            {
                IInteractable candidate = col.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(gameObject)) continue;

                Vector3 targetPos = col.bounds.center;
                Vector3 toTarget = targetPos - origin.position;
                float dist = toTarget.magnitude;
                float anglePenalty = Vector3.Angle(origin.forward, toTarget) * 0.03f;
                float score = dist + anglePenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void OnGUI()
        {
            if (currentTarget == null) return;

            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                promptStyle.normal.textColor = new Color(0.92f, 0.97f, 1f, 1f);
            }

            string label = $"[E] {currentTarget.PromptText}";
            float width = 360f;
            float height = 40f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.62f, width, height);
            GUI.Box(rect, label, promptStyle);
        }
    }
}
