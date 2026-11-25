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
        private bool isInteractionBlocked;

        public bool IsInteractionBlocked
        {
            get => isInteractionBlocked;
            set
            {
                isInteractionBlocked = value;
                if (value) currentTarget = null;
            }
        }

        private void Awake()
        {
            if (viewOrigin == null && Camera.main != null)
            {
                viewOrigin = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (isInteractionBlocked)
            {
                currentTarget = null;
                return;
            }

            if (viewOrigin == null && Camera.main != null)
            {
                viewOrigin = Camera.main.transform;
            }

            currentTarget = FindBestInteractable();

            if (currentTarget != null && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                currentTarget.Interact(gameObject);
                currentTarget = isInteractionBlocked ? null : FindBestInteractable();
            }
        }

        private IInteractable FindBestInteractable()
        {
            Transform origin = viewOrigin != null ? viewOrigin : transform;

            // 1. Prefer closest interactable along the direct look ray (ignores non-interactable geometry overlaps)
            RaycastHit[] rayHits = Physics.RaycastAll(origin.position, origin.forward, interactRange, ~0, QueryTriggerInteraction.Collide);
            IInteractable lookedBest = null;
            float lookedBestDist = float.MaxValue;
            foreach (RaycastHit hit in rayHits)
            {
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
                IInteractable looked = hit.collider.GetComponentInParent<IInteractable>();
                if (looked != null && looked.CanInteract(gameObject) && hit.distance < lookedBestDist)
                {
                    lookedBestDist = hit.distance;
                    lookedBest = looked;
                }
            }
            if (lookedBest != null)
            {
                return lookedBest;
            }

            // 2. Fallback to closest/most-centered available interactable within proximity radius
            // Sample from player body center so low floor/table pickups (e.g. Energy Cells) are always reachable.
            Vector3 proximityCenter = Vector3.Lerp(transform.position, origin.position, 0.5f);
            Collider[] nearby = Physics.OverlapSphere(proximityCenter, interactRange, ~0, QueryTriggerInteraction.Collide);
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
            Vector2 textSize = promptStyle.CalcSize(new GUIContent(label));
            float width = Mathf.Max(360f, textSize.x + 48f);
            float height = Mathf.Max(42f, textSize.y + 16f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.62f, width, height);
            GUI.Box(rect, label, promptStyle);
        }
    }
}
