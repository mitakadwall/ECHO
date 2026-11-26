using System.Collections;
using UnityEngine;
using Eco.Player;

namespace Eco.Tutorial
{
    /// <summary>
    /// Automatically unlocks and slides open a module exit door (saida M2..M6)
    /// when the player approaches within triggerDistance.
    /// </summary>
    public class TutorialAutoDoor : MonoBehaviour
    {
        [Header("Proximity")]
        [SerializeField] private float triggerDistance = 5.5f;

        [Header("Unlock & Open")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 3.85f, 0f);
        [SerializeField] private float openDuration = 1.15f;
        [SerializeField] private Color unlockedColor = new Color(0.15f, 0.85f, 1.0f, 1f);
        [SerializeField] private float unlockedIntensity = 3.2f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Vector3 closedLocalPos;
        private Vector3 doorWorldCenter;
        private Transform playerTransform;
        private bool isOpeningOrOpen;
        private bool isOpen;

        public bool IsOpen => isOpen;
        public bool IsOpeningOrOpen => isOpeningOrOpen;

        private void Awake()
        {
            closedLocalPos = transform.localPosition;
            Renderer r = GetComponent<Renderer>();
            doorWorldCenter = r != null ? r.bounds.center : transform.position;
        }

        private void Update()
        {
            if (isOpeningOrOpen) return;

            if (playerTransform == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
                else return;
            }

            Vector3 p = playerTransform.position;
            Vector3 flatDiff = new Vector3(p.x - doorWorldCenter.x, 0f, p.z - doorWorldCenter.z);
            float verticalDiff = Mathf.Abs(p.y - doorWorldCenter.y);

            if (flatDiff.magnitude <= triggerDistance && verticalDiff <= 3.5f)
            {
                OpenDoor();
            }
        }

        public void OpenDoor()
        {
            if (isOpeningOrOpen) return;
            isOpeningOrOpen = true;
            UnlockIndicatorColor();
            StartCoroutine(SlideOpenRoutine());
        }

        private void UnlockIndicatorColor()
        {
            Renderer r = GetComponent<Renderer>();
            if (r == null) return;

            Material[] mats = r.sharedMaterials;
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.Contains("Red_NEXUS"))
                {
                    r.GetPropertyBlock(propBlock, i);
                    propBlock.SetColor(BaseColorId, unlockedColor);
                    propBlock.SetColor(EmissionColorId, unlockedColor * unlockedIntensity);
                    r.SetPropertyBlock(propBlock, i);
                }
            }
        }

        private IEnumerator SlideOpenRoutine()
        {
            Vector3 startPos = closedLocalPos;
            Vector3 targetPos = closedLocalPos + openOffset;
            float elapsed = 0f;

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / openDuration);
                float eased = t * t * (3f - 2f * t);
                transform.localPosition = Vector3.Lerp(startPos, targetPos, eased);
                yield return null;
            }

            transform.localPosition = targetPos;
            isOpen = true;
        }
    }
}
