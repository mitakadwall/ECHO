using UnityEngine;

namespace Eco.Enemies.Boss
{
    /// <summary>
    /// Expanding emissive ground ring representing Carcass-0's hydraulic ground-slam shockwave.
    /// </summary>
    public class BossShockwaveEffect : MonoBehaviour
    {
        private LineRenderer ring;
        private float maxRadius;
        private float duration;
        private float elapsed;
        private Color baseColor;

        private const int Segments = 48;

        public static void Spawn(Vector3 center, float radius, bool isOverheatedPhase, float duration = 0.55f)
        {
            GameObject go = new GameObject("CarcassShockwaveRing");
            go.transform.position = center + Vector3.up * 0.08f;
            BossShockwaveEffect fx = go.AddComponent<BossShockwaveEffect>();
            Color c = isOverheatedPhase
                ? new Color(1.0f, 0.24f, 0.06f, 1f)
                : new Color(1.0f, 0.62f, 0.14f, 1f);
            fx.Initialize(radius, duration, c);
        }

        private void Initialize(float radius, float dur, Color color)
        {
            maxRadius = Mathf.Max(1f, radius);
            duration = Mathf.Max(0.15f, dur);
            baseColor = color;

            ring = gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.startWidth = 0.28f;
            ring.endWidth = 0.28f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;

            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s != null)
            {
                Material mat = new Material(s);
                mat.color = color;
                ring.sharedMaterial = mat;
            }

            UpdateRing(0.05f, 1f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float currentRadius = Mathf.Lerp(0.4f, maxRadius, eased);
            float alpha = 1f - t;

            UpdateRing(currentRadius, alpha);

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateRing(float radius, float alpha)
        {
            if (ring == null) return;

            for (int i = 0; i < Segments; i++)
            {
                float angle = (i / (float)Segments) * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            float width = Mathf.Lerp(0.04f, 0.32f, alpha);
            ring.startWidth = width;
            ring.endWidth = width;

            Color c = baseColor;
            c.a = alpha;
            ring.startColor = c;
            ring.endColor = c;
        }
    }
}
